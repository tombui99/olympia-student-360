const assert = require('node:assert/strict');
const { test } = require('node:test');
const { readFileSync } = require('node:fs');
const ts = require('typescript');
require('@angular/compiler');
const { createEnvironmentInjector, runInInjectionContext } = require('@angular/core');
const { ActivatedRoute, Router, convertToParamMap } = require('@angular/router');
const { of } = require('rxjs');

require.extensions['.ts'] = (loaded, filename) => loaded._compile(ts.transpileModule(readFileSync(filename, 'utf8'), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, experimentalDecorators: true },
}).outputText, filename);

const { activityMonths, activityWeeks, normalizeActivityPeriod } = require('../src/app/reports/activity-period.ts');
const { LearningReviewReportComponent } = require('../src/app/reports/learning-review-report.component.ts');
const { LearningReportsService } = require('../src/app/reports/learning-reports.service.ts');
const { LanguageService } = require('../src/app/core/language/language.service.ts');
const { ToastService } = require('../src/app/core/toast/toast.service.ts');
const { ReportExcelExportService } = require('../src/app/reports/report-excel-export.service.ts');
const { StaticReportsComponent } = require('../src/app/reports/static-reports.component.ts');
const { ReportBuilderService } = require('../src/app/reports/report-builder.service.ts');
const { WorkspaceFiltersService, defaultFilterSet } = require('../src/app/workspace/workspace-filters.service.ts');
const { AuthenticationService } = require('../src/app/core/auth/auth.service.ts');
const { DashboardOverviewService } = require('../src/app/dashboard/dashboard-overview.service.ts');
const { StudentsService } = require('../src/app/students/students.service.ts');
const { ScoreReportComponent } = require('../src/app/reports/score-report.component.ts');
const { ScoreReportsService } = require('../src/app/reports/score-reports.service.ts');
const { LeadershipReportComponent } = require('../src/app/reports/leadership-report.component.ts');
const { LeadershipReportsService } = require('../src/app/reports/leadership-reports.service.ts');
const { ActivityPeriodFilterComponent } = require('../src/app/reports/activity-period-filter.component.ts');

test('week options require a month, follow its dates, and reset when the month changes or is cleared', () => {
  const injector = createEnvironmentInjector([{ provide: LanguageService, useValue: { t: key => key } }]);
  const component = runInInjectionContext(injector, () => new ActivityPeriodFilterComponent());
  const selections = [];
  const subscription = component.periodChange.subscribe(selection => selections.push(selection));
  try {
    component.academicYear = '2026-2027';
    assert.deepEqual(component.weeks(), []);
    component.selectWeek('2026-10-26');
    assert.deepEqual(selections.at(-1), { month: null, weekStart: null });
    component.selectMonth(10);
    Object.assign(component, selections.at(-1));
    assert.deepEqual(component.weeks().map(week => week.value),
      ['2026-09-28', '2026-10-05', '2026-10-12', '2026-10-19', '2026-10-26']);
    component.selectWeek('2026-10-26');
    Object.assign(component, selections.at(-1));
    component.selectMonth(11);
    assert.deepEqual(selections.at(-1), { month: 11, weekStart: null });
    Object.assign(component, selections.at(-1));
    assert.deepEqual(component.weeks().map(week => week.value),
      ['2026-10-26', '2026-11-02', '2026-11-09', '2026-11-16', '2026-11-23', '2026-11-30']);
    component.selectWeek('2026-11-02');
    Object.assign(component, selections.at(-1));
    component.selectMonth(null);
    assert.deepEqual(selections.at(-1), { month: null, weekStart: null });
    assert.deepEqual(normalizeActivityPeriod('2026-2027', { month: null, weekStart: '2026-11-02' }),
      { month: null, weekStart: null });
    assert.deepEqual(normalizeActivityPeriod('2026-2027', { month: 13, weekStart: '2026-11-02' }),
      { month: null, weekStart: null });
  } finally { subscription.unsubscribe(); injector.destroy(); }
});

for (const reportKey of ['school-subject-grade-average', 'school-score-histogram', 'school-top-students', 'school-bottom-students',
  'subject-band-by-subject', 'subject-band-by-grade', 'subject-band-by-class', 'subject-assessment-average', 'subject-class-results',
  'school-learning-engagement', 'school-assessment-results', 'school-current-course-score']) {
  test(`${reportKey}: shared activity filters update requests and exports and survive other filter resets`, async () => {
    const requests = [];
    const navigations = [];
    let exported;
    const leadership = ['school-learning-engagement', 'school-assessment-results'].includes(reportKey);
    const current = reportKey === 'school-current-course-score';
    const result = { aggregateAvailable: true, totalCount: 21, page: 1, pageSize: 20,
      rows: [{ key: 'test', label: 'Test', name: 'Test', scoreCount: 1 }], unit: 'score10' };
    const reportService = {
      options: () => of({ courses: [], cohorts: [], subjects: [], grades: [], homerooms: [], subaccounts: [], sections: [], assessmentCategories: ['all'] }),
      run: request => { requests.push(structuredClone(request)); return of({ ...result, page: request.page ?? 1 }); },
    };
    const injector = createEnvironmentInjector([
      { provide: LanguageService, useValue: { t: key => key } },
      { provide: ToastService, useValue: { error: message => assert.fail(message) } },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } },
      { provide: Router, useValue: { navigate: (_, options) => { navigations.push(options); return Promise.resolve(true); } } },
      { provide: leadership ? LeadershipReportsService : ScoreReportsService, useValue: reportService },
      { provide: ReportExcelExportService, useValue: {
        collectPages: async fetch => { await fetch(1, 1000); return result.rows; },
        download: async definition => { exported = definition; },
      } },
    ]);
    const component = runInInjectionContext(injector, () => new (leadership ? LeadershipReportComponent : ScoreReportComponent)());
    try {
      Object.assign(component, { reportKey, academicYear: '2026-2027', term: 'hk1', activityPeriod: { month: 10, weekStart: '2026-10-26' } });
      component.ngOnInit();
      assert.equal(requests.at(-1).month, current ? null : 10);
      assert.equal(requests.at(-1).weekStart, current ? null : '2026-10-26');
      component.activityPeriod = { month: 11, weekStart: '2026-11-02' };
      component.ngOnChanges({ activityPeriod: {} });
      assert.equal(requests.at(-1).month, current ? null : 11);
      assert.equal(requests.at(-1).weekStart, current ? null : '2026-11-02');
      component.resetFilters();
      assert.equal(requests.at(-1).month, current ? null : 11);
      assert.equal(navigations.at(-1).queryParams.activityMonth, 11);
      assert.equal(navigations.at(-1).queryParams.activityWeekStart, '2026-11-02');
      assert.equal(navigations.at(-1).queryParams.report, reportKey);
      if (leadership) {
        component.changePage(2);
        assert.equal(requests.at(-1).page, 2);
        assert.equal(requests.at(-1).includeSummary, false);
      }
      await component.exportExcel();
      if (!current) {
        assert.ok(exported.filters.some(([key, value]) => key === 'reports.activityPeriod.week' && value === '02/11/2026 – 08/11/2026'));
        assert.equal(navigations.at(-1).queryParams[leadership ? 'leadWeekStart' : 'scoreWeekStart'], '2026-11-02');
      }
    } finally { component.ngOnDestroy(); injector.destroy(); }
  });
}

test('month and week choices follow the September school year, calendar boundaries, and leap days', () => {
  const months = activityMonths('2026-2027');
  assert.deepEqual(months[0], { value: 9, label: '09/2026' });
  assert.deepEqual(months[4], { value: 1, label: '01/2027' });
  assert.deepEqual(months.at(-1), { value: 8, label: '08/2027' });
  const weeks = activityWeeks('2026-2027');
  assert.deepEqual(weeks[0], { value: '2026-08-31', label: '31/08/2026 – 06/09/2026' });
  assert.equal(weeks.at(-1).value, '2027-08-30');
  assert.ok(activityWeeks('2023-2024', 2).some(week => week.label === '26/02/2024 – 03/03/2024'));
  assert.ok(activityWeeks('2026-2027', 1).some(week => week.value === '2026-12-28'));
  assert.ok(activityWeeks('2026-2027', 10).some(week => week.value === '2026-10-26'));
  assert.ok(activityWeeks('2026-2027', 11).some(week => week.value === '2026-10-26'));
  assert.deepEqual(activityWeeks('invalid'), []);
});

test('changing month or school year clears incompatible weeks and rejects malformed URL dates', () => {
  assert.deepEqual(normalizeActivityPeriod('2026-2027', { month: 11, weekStart: '2026-10-26' }),
    { month: 11, weekStart: '2026-10-26' });
  for (const weekStart of ['2026-10-19', '2026-11-03', 'bad-date', '2025-11-03']) {
    assert.deepEqual(normalizeActivityPeriod('2026-2027', { month: 11, weekStart }), { month: 11, weekStart: null });
  }
  assert.deepEqual(normalizeActivityPeriod('2027-2028', { month: 10, weekStart: '2026-10-26' }),
    { month: 10, weekStart: null });
});

test('learning diligence restores activity filters after loading its year and sends them to metrics and exports', async () => {
  const requests = [];
  const navigations = [];
  let exported;
  const injector = createEnvironmentInjector([
    { provide: LanguageService, useValue: { t: key => key } },
    { provide: ToastService, useValue: {} },
    { provide: AuthenticationService, useValue: { canAccess: () => true } },
    { provide: DashboardOverviewService, useValue: {} },
    { provide: StudentsService, useValue: {} },
    { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({
      report: 'learning-diligence', ldMonth: '10', ldWeekStart: '2026-10-26',
    }) } } },
    { provide: Router, useValue: { navigate: (_, options) => { navigations.push(options); return Promise.resolve(true); } } },
    { provide: WorkspaceFiltersService, useValue: {
      periodOptions: () => of({ academicYears: ['2026-2027', '2027-2028'], terms: ['hk1', 'hk2'] }),
      load: () => of({ ...defaultFilterSet(), year: '2026-2027', semester: 'hk1' }), save: () => {},
    } },
    { provide: ReportBuilderService, useValue: { loadMetricVariables: params => {
      requests.push(params);
      return of([{ id: 'canvas-assignments-submitted', value: 2 }, { id: 'canvas-assignments-missing', value: 1 }]);
    } } },
    { provide: ReportExcelExportService, useValue: { download: async definition => { exported = definition; } } },
  ]);
  const component = runInInjectionContext(injector, () => new StaticReportsComponent());
  try {
    component.loadClassStatusSettings = () => {};
    component.ngOnInit();
    assert.equal(requests.at(-1).month, '10');
    assert.equal(requests.at(-1).weekStart, '2026-10-26');
    component.onActivityPeriodChange({ month: 11, weekStart: '2026-10-26' });
    assert.equal(requests.at(-1).month, '11');
    assert.equal(navigations.at(-1).queryParams.activityWeekStart, '2026-10-26');
    await component.exportStaticReport();
    assert.equal(exported.filters.find(([label]) => label === 'reports.activityPeriod.week')[1], '26/10/2026 – 01/11/2026');
    component.selectedYear = '2027-2028';
    component.onPeriodChange();
    assert.equal(requests.at(-1).weekStart, undefined);
    assert.equal(navigations.at(-1).queryParams.activityWeekStart, null);
    const available = component.reportGroups.filter(group => ['learning', 'school-leadership', 'subject-leadership'].includes(group.id))
      .flatMap(group => group.items.map(item => item.id));
    const eligible = [];
    for (const report of available) {
      component.activeReport.set(report);
      assert.equal(component.supportsActivityPeriod(), !['support-classes', 'school-current-course-score'].includes(report));
      if (component.supportsActivityPeriod()) eligible.push(report);
    }
    assert.equal(eligible.length, 16);
    assert.ok(component.periodLabel().includes('2027–2028'));
  } finally { component.ngOnDestroy(); injector.destroy(); }
});

for (const reportKey of ['missing-frequency', 'homework-achievement', 'reading-completion', 'grading-coverage']) {
  test(`${reportKey}: month/week survive URL restoration, pagination and applied-result export, and reset clears them`, async () => {
    const requests = [];
    const navigations = [];
    let exported;
    const route = { snapshot: { queryParamMap: convertToParamMap({
      lrReport: reportKey, lrMonth: '10', lrWeekStart: '2026-10-26', lrCourse: '100', lrAssignment: '200',
    }) } };
    const injector = createEnvironmentInjector([
      { provide: LanguageService, useValue: { t: key => key } },
      { provide: ToastService, useValue: { error: message => assert.fail(message) } },
      { provide: ActivatedRoute, useValue: route },
      { provide: Router, useValue: { navigate: (_, options) => { navigations.push(options); return Promise.resolve(true); } } },
      { provide: LearningReportsService, useValue: {
        options: () => of({ courses: [], subaccounts: [], assignmentGroups: [], assessmentCategories: ['all'] }),
        assignments: () => of([{ id: 200, courseId: 100, name: 'Task' }]),
        run: query => {
          requests.push(structuredClone(query));
          return of({ rows: [], totalCount: 31, aggregateAvailable: true, page: query.page, pageSize: query.pageSize ?? 15 });
        },
      } },
      { provide: ReportExcelExportService, useValue: {
        collectPages: async fetchPage => { await fetchPage(1, 1000); return []; },
        download: async definition => { exported = definition; },
      } },
    ]);
    const component = runInInjectionContext(injector, () => new LearningReviewReportComponent());
    try {
      Object.assign(component, { reportKey, academicYear: '2026-2027', term: 'hk1' });
      component.ngOnInit();
      assert.equal(requests.at(-1).month, 10);
      assert.equal(requests.at(-1).weekStart, '2026-10-26');
      component.changePage(2);
      assert.equal(requests.at(-1).page, 2);
      assert.equal(navigations.at(-1).queryParams.lrWeekStart, '2026-10-26');
      component.query.month = 11;
      component.query.weekStart = '2026-11-02';
      await component.exportExcel();
      assert.equal(requests.at(-1).month, 10);
      assert.equal(requests.at(-1).weekStart, '2026-10-26');
      assert.equal(exported.filters.find(([label]) => label === 'reports.activityPeriod.week')[1], '26/10/2026 – 01/11/2026');
      component.resetFilters();
      assert.equal(component.query.month, null);
      assert.equal(component.query.weekStart, null);
      assert.equal(navigations.at(-1).queryParams.lrWeekStart, null);
      route.snapshot.queryParamMap = convertToParamMap({ lrReport: reportKey, lrWeekStart: '2026-10-27', lrMonth: '13' });
      assert.equal(component.queryFromUrl().month, null);
      assert.equal(component.queryFromUrl().weekStart, null);
    } finally { component.ngOnDestroy(); injector.destroy(); }
  });
}
