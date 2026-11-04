const assert = require('node:assert/strict');
const { test } = require('node:test');
const { readFileSync } = require('node:fs');
const ts = require('typescript');
require('@angular/compiler');
const { createEnvironmentInjector, runInInjectionContext } = require('@angular/core');
const { ActivatedRoute, Router, convertToParamMap } = require('@angular/router');
const { of, Subject } = require('rxjs');
require.extensions['.ts'] = (loaded, filename) => loaded._compile(ts.transpileModule(readFileSync(filename, 'utf8'), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, experimentalDecorators: true },
}).outputText, filename);
const { LanguageService } = require('../src/app/core/language/language.service.ts');
const { ToastService } = require('../src/app/core/toast/toast.service.ts');
const { AuthenticationService } = require('../src/app/core/auth/auth.service.ts');
const { ReportExcelExportService } = require('../src/app/reports/report-excel-export.service.ts');
const { ReportBuilderService } = require('../src/app/reports/report-builder.service.ts');
const { WorkspaceFiltersService } = require('../src/app/workspace/workspace-filters.service.ts');
const { StudentsService } = require('../src/app/students/students.service.ts');
const { SubjectSupportReportsService } = require('../src/app/reports/subject-support-reports.service.ts');
const { StaticReportsComponent } = require('../src/app/reports/static-reports.component.ts');
const { ScoreReportComponent } = require('../src/app/reports/score-report.component.ts');
const { ScoreReportsService } = require('../src/app/reports/score-reports.service.ts');
const { LeadershipReportComponent } = require('../src/app/reports/leadership-report.component.ts');
const { LeadershipReportsService } = require('../src/app/reports/leadership-reports.service.ts');
const { LearningReviewReportComponent } = require('../src/app/reports/learning-review-report.component.ts');
const { LearningReportsService } = require('../src/app/reports/learning-reports.service.ts');

const language = { t: key => key, language: () => 'en' };
for (const [Component, Service, key, prefix] of [
  [ScoreReportComponent, ScoreReportsService, 'subject-assessment-average', 'score'],
  [LeadershipReportComponent, LeadershipReportsService, 'school-assessment-results', 'lead'],
  [LearningReviewReportComponent, LearningReportsService, 'homework-achievement', 'lr'],
]) {
  test(`${prefix}: individual HS1 selection survives URL loading, requests, pending edits and export`, async () => {
    const requests = [];
    let exported;
    const result = { aggregateAvailable: true, rows: [{ key: 'one', label: 'Math', scoreCount: 1, averageScore: 8 }],
      total: { scoreCount: 1 }, totalCount: 1, page: 1, pageSize: 20 };
    const injector = createEnvironmentInjector([
      { provide: LanguageService, useValue: language },
      { provide: ToastService, useValue: { error: message => assert.fail(message) } },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({
        [`${prefix}Report`]: key, [`${prefix}Category`]: 'HS1.2',
      }) } } },
      { provide: Router, useValue: { navigate: () => Promise.resolve(true) } },
      { provide: Service, useValue: { run: query => { requests.push(structuredClone(query)); return of(result); } } },
      { provide: ReportExcelExportService, useValue: {
        collectPages: async fetch => { await fetch(1, 1000); return result.rows; },
        download: async definition => { exported = definition; },
      } },
    ]);
    try {
      const component = runInInjectionContext(injector, () => new Component());
      Object.assign(component, { reportKey: key, academicYear: '2026-2027', term: 'hk1' });
      component.query = component.queryFromUrl(); // Options deliberately haven't loaded yet.
      assert.equal(component.query.assessmentCategory, 'HS1.2');
      assert.ok(component.categoryChoices().includes('HS1.2'));
      component.run();
      assert.equal(requests.at(-1).assessmentCategory, 'HS1.2');
      component.query.assessmentCategory = 'HS1.4'; // Unapplied edit must not alter the export.
      await component.exportExcel();
      assert.ok(exported.filters.some(filter => filter[1] === 'reports.leadership.category.HS1.2'));
      assert.ok(requests.every(request => request.assessmentCategory === 'HS1.2'));
    } finally { injector.destroy(); }
  });
}

test('subject support keeps summary, drilldown and export in the same subject and period', async () => {
  let exported;
  const details = [];
  const pending = new Subject();
  let deferSummary = false;
  const cohort = { grade: '8', homeroom: '8A', students: 4, atRisk: 2, scoredStudents: 3,
    noScoreStudents: 1, averageCanvasScorePercentage: 41.7 };
  const summary = { periodYear: '2026-2027', periodTerm: 'hk1', subjectId: 20, subjectName: 'Math',
    lowScoreThreshold: 65, subjects: [{ id: 20, name: 'Math' }], gradeDistribution: [cohort], classDistribution: [cohort] };
  const injector = createEnvironmentInjector([
    { provide: LanguageService, useValue: language },
    { provide: ToastService, useValue: { error: message => assert.fail(message) } },
    { provide: AuthenticationService, useValue: { canAccess: () => true } },
    { provide: SubjectSupportReportsService, useValue: {
      summary: () => deferSummary ? pending : of(summary),
      students: query => { details.push(query); return of({ items: [], page: 1, totalCount: 2, totalPages: 1 }); },
    } },
    { provide: StudentsService, useValue: {} }, // General at-risk/list requests must never be used here.
    { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } },
    { provide: Router, useValue: { navigate: () => Promise.resolve(true) } },
    { provide: WorkspaceFiltersService, useValue: { save: () => {} } },
    { provide: ReportBuilderService, useValue: {} },
    { provide: ReportExcelExportService, useValue: { download: async definition => { exported = definition; } } },
  ]);
  const originalWindow = globalThis.window;
  globalThis.window = { requestAnimationFrame: callback => callback(), setTimeout: () => 1, clearTimeout: () => {} };
  try {
    const component = runInInjectionContext(injector, () => new StaticReportsComponent());
    component.activeReport.set('support-classes');
    component.selectedYear = '2026-2027'; component.selectedTerm = 'hk1';
    component.loadReportData();
    assert.equal(component.selectedSupportSubjectId(), 20);
    assert.equal(component.classRows()[0].noScoreStudents, 1);
    assert.equal(component.classStatus({ ...component.classRows()[0], scoredStudents: 0 }), null);
    component.openClassDrilldown(component.classRows()[0], 'support');
    assert.deepEqual(details[0], { subjectId: 20, year: '2026-2027', term: 'hk1', grade: '8', homeroom: '',
      supportOnly: true, search: '', page: 1, pageSize: 20 });
    component.selectedYear = '2027-2028'; // A drilldown always retains the applied context.
    component.loadClassDrilldown(1);
    assert.equal(details[1].year, '2026-2027');
    component.selectedYear = '2026-2027';
    await component.exportStaticReport();
    assert.ok(exported.filters.some(filter => filter[1] === 'Math'));
    assert.ok(exported.filters.some(filter => filter[1] === '65%'));
    assert.ok(exported.columns.some(column => column.header === 'reports.classes.noScores' && column.value(exported.rows[0]) === 1));
    deferSummary = true;
    component.onSupportSubjectChange(30);
    assert.equal(component.canExportStaticReport(), false); // Don't export old Math rows under a pending subject.
  } finally { injector.destroy(); globalThis.window = originalWindow; }
});
