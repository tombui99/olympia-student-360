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
const { SKIP_GLOBAL_API_LOADING } = require('../src/app/core/loading/api-loading.interceptor.ts');
const { TranscriptSettingsComponent } = require('../src/app/transcripts/transcript-settings.component.ts');
const { StudentTranscriptComponent } = require('../src/app/transcripts/student-transcript.component.ts');
const { TranscriptsComponent } = require('../src/app/transcripts/transcripts.component.ts');
const { createTranscriptDraft, hasTranscriptCustomizations, prepareTranscriptRecord, transcriptRecordError } = require('../src/app/transcripts/transcript-edit.ts');

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
    { provide: HttpClient, useValue: { get: (url, options) => { calls.push({ url, params: options.params, context: options.context }); return of({}); } } },
  ]);
  try {
    const service = runInInjectionContext(injector, () => new TranscriptsService());
    service.getOverall(undefined, 2, 10, '2026-2027', '10');
    service.getOverall(7541);
    service.getGradeDetails('2026-2027', '10', 3, 25, '', '', 2);
    for (const call of calls) assert.deepEqual(call.params.excludedSubjects, ['music', 'physical-education']);
    assert.equal(calls[0].context.get(SKIP_GLOBAL_API_LOADING), true);
    assert.equal(calls[1].context.get(SKIP_GLOBAL_API_LOADING), true);
    assert.equal(calls[0].params.academicYear, '2026-2027');
    assert.equal(calls[0].params.gradeLevel, '10');
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
    { provide: TranscriptsService, useValue: { getCatalog: () => of({ subjectOptions: options, years: [] }) } },
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

test('all four student PDF formats receive the same exclusions and selected periods', async () => {
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
    exporting: signal(false), selectedYears: signal(['2026-2027']), selectedSemester: signal('all'),
    activeTranscriptType: signal('international'), scoreScale: signal('letter'), editDraft: signal(null), resetSaving: signal(false),
  });
  const printed = [];
  component.pdfService = { download: async (record, options) => printed.push(options) };
  component.toast = { error: message => assert.fail(message) };
  try {
    const formats = ['bilingual', 'bilingual-gpa', 'international', 'ib'];
    for (const format of formats) await component.exportPdf(format);
    assert.deepEqual(printed, formats.map(format => ({ format, academicYears: ['2026-2027'],
      semester: 'all', excludedSubjectCodes: ['music'] })));
    assert.deepEqual(component.subjectsForSemester(grade, 'semester1').map(subject => subject.subjectCode), ['math']);
    component.loading.set(true);
    await component.exportPdf();
    assert.equal(printed.length, 4);
    component.loading.set(false); component.editDraft.set({ revision: 1, years: [] });
    await component.exportPdf();
    assert.equal(printed.length, 4, 'unsaved changes cannot silently be omitted from an export');
    fixture.preferences.saveExcludedSubjects([]);
    assert.deepEqual(component.subjectsForSemester(grade, 'semester1').map(subject => subject.subjectCode), ['math', 'music']);
  } finally { fixture.injector.destroy(); }
});


test('subject grade filters and bulk selection affect only the matching subjects', () => {
  const fixture = preferencesFixture();
  const injector = createEnvironmentInjector([
    { provide: TranscriptPreferencesService, useValue: fixture.preferences },
    { provide: LanguageService, useValue: { t: key => key } },
    { provide: TranscriptsService, useValue: {} },
  ]);
  try {
    const component = runInInjectionContext(injector, () => new TranscriptSettingsComponent());
    component.options.set([
      { subjectCode: 'math', subject: 'Math', gradeLevels: ['11', '12'] },
      { subjectCode: 'physics', subject: 'Physics', gradeLevels: ['12'] },
      { subjectCode: 'music', subject: 'Music', gradeLevels: ['9'] },
    ]);
    component.draftExclusions.set(['music']);
    component.selectedGrade.set('12');
    component.selectFiltered(true);
    assert.deepEqual(new Set(component.draftExclusions()), new Set(['music', 'math', 'physics']));
    component.search.set('math');
    component.selectFiltered(false);
    assert.deepEqual(new Set(component.draftExclusions()), new Set(['music', 'physics']));
    assert.deepEqual(component.gradeOptions(), ['9', '11', '12']);
  } finally { injector.destroy(); fixture.injector.destroy(); }
});


test('numeric bilingual results use the same percentage-to-ten-point scale as PDFs, including low marks', () => {
  const component = Object.create(StudentTranscriptComponent.prototype);
  assert.equal(component.tenPoint(5), '0.5');
  assert.equal(component.tenPoint(0), '0.0');
  assert.equal(component.tenPoint(95), '9.5');
  assert.equal(component.tenPoint(null), '—');
});


test('school summaries never request marks before selecting both year and grade', () => {
  const calls = [];
  const component = Object.assign(Object.create(TranscriptsComponent.prototype), {
    selectedYear: signal(''), selectedGrade: signal(''), loading: signal(false), error: signal(false),
    loadingSubjectPage: signal(''), transcript: signal(null),
    service: { getOverall: (...args) => { calls.push(args); return of({ years: [] }); } },
  });
  component.load();
  component.selectedYear.set('2026-2027'); component.load();
  assert.equal(calls.length, 0);
  component.selectedGrade.set('12'); component.load();
  assert.deepEqual(calls, [[undefined, 1, 10, '2026-2027', '12']]);
  assert.equal(component.selectedYear(), '2026-2027');
});

function editableTranscript() {
  return { canEditRecord: true, studentProfileId: 1,
    record: { revision: 7, notes: 'Keep these notes', ibCorePoints: 2,
      graduationExamDate: '2027-05-20', years: [
        { academicYear: '2025-2026', gradeLevel: '11', showAverage: false,
          credits: [{ subjectCode: 'history', credit: .5, englishName: 'History' }],
          ibSubjects: [{ subjectCode: 'history', subjectName: 'History', level: 'SL', term1: '5' }] },
      ] },
    years: [{ academicYear: '2026-2027', grades: [{ gradeLevel: '12', subjects: [
      { subjectCode: 'math', subject: 'Toán/Math', englishName: 'Math', credit: 1, semester1Final: 85 },
    ] }] }], academicYearOptions: ['2025-2026', '2026-2027'] };
}

test('inline drafts leave imported scores untouched and preserve edits from other years and formats', () => {
  const source = editableTranscript(), original = structuredClone(source);
  const draft = createTranscriptDraft(source);
  const year = draft.years.find(year => year.academicYear === '2026-2027');
  year.credits[0].credit = .5; year.credits[0].englishName = 'Mathematics';
  draft.transcriptName = 'Official Name';
  const payload = prepareTranscriptRecord(draft, source);
  assert.deepEqual(source, original, 'cancel can discard a draft without restoring mutated source data');
  assert.equal(payload.revision, 7);
  assert.equal(payload.notes, 'Keep these notes');
  assert.equal(payload.graduationExamDate, '2027-05-20');
  assert.deepEqual(payload.years[0], source.record.years[0]);
  assert.equal(payload.years[1].credits[0].credit, .5);
  assert.deepEqual(payload.years[1].ibSubjects, [], 'Canvas percentages never become IB results');
  assert.equal('semester1Final' in payload.years[1].credits[0], false);
});

test('inline IB entries keep name-only corrections, official-only subjects and incomplete real marks', () => {
  const source = editableTranscript(), draft = createTranscriptDraft(source);
  const year = draft.years.find(year => year.academicYear === '2026-2027');
  year.ibSubjects[0].subjectName = 'Mathematics: Analysis and Approaches';
  let payload = prepareTranscriptRecord(draft, source);
  assert.equal(payload.years[1].ibSubjects[0].subjectName, 'Mathematics: Analysis and Approaches');
  assert.equal(payload.years[1].gradeLevel, '12');
  year.ibSubjects[0].predictedGrade = '7'; year.ibSubjects[0].level = 'HL';
  payload = prepareTranscriptRecord(draft, source);
  assert.equal(transcriptRecordError(payload), null);
  year.ibSubjects[0].term1 = '85';
  assert.equal(transcriptRecordError(prepareTranscriptRecord(draft, source)), 'transcript.editor.error');
  year.ibSubjects[0].term1 = '6'; year.gradeLevel = '10';
  assert.equal(transcriptRecordError(prepareTranscriptRecord(draft, source)), 'transcript.editor.gradeRequired');
});

test('inline editing blocks leaving with an active draft and cancel restores the original selection', () => {
  const messages = [];
  const component = Object.assign(Object.create(StudentTranscriptComponent.prototype), {
    editDraft: signal({ revision: 7, years: [] }), savingRecord: signal(false), resetSaving: signal(false), editError: signal(''), gradeCorrectionYear: signal(''),
    selectedYears: signal(['2027-2028']), yearsBeforeEditing: ['2026-2027'],
    pendingTranscriptReload: false, toast: { error: message => messages.push(message) }, t: key => key,
  });
  assert.equal(component.canLeaveTranscript(), false);
  assert.deepEqual(messages, ['transcript.editor.finishEditing']);
  component.cancelEditing();
  assert.equal(component.canLeaveTranscript(), true);
  assert.deepEqual(component.selectedYears(), ['2026-2027']);
  assert.equal(component.editDraft(), null);
  component.resetSaving.set(true);
  assert.equal(component.canLeaveTranscript(), false);
  assert.equal(messages.at(-1), 'transcript.reset.inProgress');
});

test('reset is only available for saved customizations, including IB entries and explicit zero values', () => {
  assert.equal(hasTranscriptCustomizations(undefined), false);
  assert.equal(hasTranscriptCustomizations({ revision: 5, years: [] }), false);
  const defaults = { revision: 5, years: [{ academicYear: '2026-2027', gradeLevel: null, showAverage: null,
    credits: [{ subjectCode: 'math', credit: 1 }], ibSubjects: [] }] };
  assert.equal(hasTranscriptCustomizations(defaults), false);
  for (const override of [{ notes: 'School note' }, { ibCorePoints: 0 }, { transcriptName: 'Official Name' },
    { graduationExamDate: '2027-05-20' }])
    assert.equal(hasTranscriptCustomizations({ ...defaults, ...override }), true);
  defaults.years[0].showAverage = false;
  assert.equal(hasTranscriptCustomizations(defaults), true);
  defaults.years[0].showAverage = null; defaults.years[0].credits[0].credit = 0;
  assert.equal(hasTranscriptCustomizations(defaults), true);
  assert.equal(hasTranscriptCustomizations(editableTranscript().record), true);
});
