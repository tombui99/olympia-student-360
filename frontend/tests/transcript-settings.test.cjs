const assert = require('node:assert/strict');
const { beforeEach, test } = require('node:test');
const { readFileSync } = require('node:fs');
const ts = require('typescript');
require('@angular/compiler');
const { createEnvironmentInjector, runInInjectionContext, signal } = require('@angular/core');
const { HttpClient } = require('@angular/common/http');
const { of } = require('rxjs');

require.extensions['.ts'] = (loaded, filename) => loaded._compile(ts.transpileModule(readFileSync(filename, 'utf8'), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, experimentalDecorators: true },
}).outputText, filename);

const { AuthenticationService } = require('../src/app/core/auth/auth.service.ts');
const { LanguageService } = require('../src/app/core/language/language.service.ts');
const { BASE_PATH } = require('../src/app/api/variables.ts');
const { TranscriptPreferencesService } = require('../src/app/transcripts/transcript-preferences.service.ts');
const { TranscriptsService } = require('../src/app/transcripts/transcripts.service.ts');
const { TranscriptSettingsComponent } = require('../src/app/transcripts/transcript-settings.component.ts');
const { StudentTranscriptComponent } = require('../src/app/transcripts/student-transcript.component.ts');

beforeEach(() => {
  const storage = new Map();
  Object.defineProperty(globalThis, 'localStorage', {
    configurable: true,
    value: {
      getItem: key => storage.get(key) ?? null,
      setItem: (key, value) => storage.set(key, value),
    },
  });
});

function preferencesFixture(username = signal('teacher-a')) {
  const injector = createEnvironmentInjector([{ provide: AuthenticationService, useValue: { username } }]);
  const preferences = runInInjectionContext(injector, () => new TranscriptPreferencesService());
  return { injector, preferences, username };
}

test('exclusions survive a reload, stay separate for each account, and can be fully restored', () => {
  const first = preferencesFixture();
  first.preferences.saveExcludedSubjects([' music ', 'MUSIC', 'physical-education']);
  const reloaded = preferencesFixture(first.username);
  try {
    assert.deepEqual(reloaded.preferences.excludedSubjectCodes(), ['music', 'physical-education']);
    first.username.set('teacher-b');
    assert.deepEqual(first.preferences.excludedSubjectCodes(), []);
    first.preferences.saveExcludedSubjects(['math']);
    first.username.set('teacher-a');
    assert.deepEqual(first.preferences.excludedSubjectCodes(), ['music', 'physical-education']);
    first.preferences.saveExcludedSubjects([]);
    const restored = preferencesFixture();
    assert.deepEqual(restored.preferences.excludedSubjectCodes(), []);
    restored.injector.destroy();
  } finally { first.injector.destroy(); reloaded.injector.destroy(); }
});

test('corrupt or unavailable browser storage does not prevent using transcript settings', () => {
  localStorage.setItem('transcripts.excludedSubjects.teacher-a', '{broken');
  const fixture = preferencesFixture();
  try {
    assert.deepEqual(fixture.preferences.excludedSubjectCodes(), []);
    localStorage.setItem = () => { throw new Error('Storage is blocked'); };
    fixture.preferences.saveExcludedSubjects(['music']);
    assert.deepEqual(fixture.preferences.excludedSubjectCodes(), ['music']);
  } finally { fixture.injector.destroy(); }
});

test('summary pages, complete student transcripts and grade records send the same exclusions', () => {
  const fixture = preferencesFixture();
  fixture.preferences.saveExcludedSubjects(['music', 'physical-education']);
  const calls = [];
  const injector = createEnvironmentInjector([
    { provide: TranscriptPreferencesService, useValue: fixture.preferences },
    { provide: BASE_PATH, useValue: 'http://test' },
    { provide: HttpClient, useValue: { get: (url, options) => { calls.push({ url, params: options.params }); return of({}); } } },
  ]);
  try {
    const service = runInInjectionContext(injector, () => new TranscriptsService());
    service.getOverall(undefined, 2);
    service.getOverall(7541);
    service.getGradeDetails('2026-2027', '10', 3, 25, '', '', 2);
    for (const call of calls) assert.deepEqual(call.params.excludedSubjects, ['music', 'physical-education']);
    assert.equal(calls[0].params.subjectPage, 2);
    assert.equal(calls[1].params.studentId, 7541);
    assert.equal(calls[1].params.subjectPage, undefined);
    assert.equal(calls[2].params.page, 3);
    assert.equal(calls[2].params.subjectPage, 2);
    fixture.preferences.saveExcludedSubjects([]);
    service.getOverall(7541);
    assert.equal(calls[3].params.excludedSubjects, undefined);
  } finally { injector.destroy(); fixture.injector.destroy(); }
});

test('settings list every subject beyond the first page and only save draft changes on Apply', () => {
  const fixture = preferencesFixture();
  fixture.preferences.saveExcludedSubjects(['music']);
  const options = Array.from({ length: 13 }, (_, index) => ({ subjectCode: `subject-${index}`, subject: `Subject ${index}` }));
  options[12] = { subjectCode: 'music', subject: 'Music' };
  const injector = createEnvironmentInjector([
    { provide: TranscriptPreferencesService, useValue: fixture.preferences },
    { provide: LanguageService, useValue: { t: key => key } },
    { provide: TranscriptsService, useValue: { getOverall: () => of({ subjectOptions: options, years: [] }) } },
  ]);
  try {
    const component = runInInjectionContext(injector, () => new TranscriptSettingsComponent());
    let open = false;
    component.dialog = () => ({ nativeElement: { showModal: () => { open = true; }, close: () => { open = false; } } });
    component.open();
    assert.equal(open, true);
    assert.equal(component.filteredOptions().length, 13);
    component.search.set('music');
    assert.deepEqual(component.filteredOptions(), [options[12]]);
    component.toggleSubject('music');
    component.close();
    assert.deepEqual(fixture.preferences.excludedSubjectCodes(), ['music']);
    component.open();
    assert.deepEqual(component.draftExclusions(), ['music']);
    component.draftExclusions.set([]);
    component.apply();
    assert.equal(open, false);
    assert.deepEqual(fixture.preferences.excludedSubjectCodes(), []);
  } finally { injector.destroy(); fixture.injector.destroy(); }
});

test('all three student transcript formats exclude subjects on screen and when exporting', () => {
  const fixture = preferencesFixture();
  fixture.preferences.saveExcludedSubjects(['music']);
  const grade = { subjects: [
    { subjectCode: 'math', semester1Final: 80, semester2Final: 85 },
    { subjectCode: 'music', semester1Final: 90, semester2Final: 95 },
  ] };
  // Keep an older, unfiltered response to cover applying settings immediately before export.
  const component = Object.assign(Object.create(StudentTranscriptComponent.prototype), {
    preferences: fixture.preferences,
    selectedStudentId: signal(7541),
    transcript: signal({ years: [{ academicYear: '2026-2027', grades: [grade] }] }),
    loading: signal(false), error: signal(false),
    activeTranscriptType: signal('international'), scoreScale: signal('letter'),
  });
  const printed = [];
  component.openPrintDialog = type => printed.push({
    type, subjects: ['semester1', 'semester2'].map(semester =>
      component.subjectsForSemester(grade, semester).map(subject => subject.subjectCode)),
  });
  try {
    for (const type of ['bilingual', 'international', 'ib']) component.exportPdf(type);
    assert.deepEqual(printed, ['bilingual', 'international', 'ib'].map(type => ({ type, subjects: [['math'], ['math']] })));
    component.loading.set(true);
    component.exportPdf();
    assert.equal(printed.length, 3);
    fixture.preferences.saveExcludedSubjects([]);
    assert.deepEqual(component.subjectsForSemester(grade, 'semester1').map(subject => subject.subjectCode), ['math', 'music']);
  } finally { fixture.injector.destroy(); }
});
