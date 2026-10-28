const assert = require('node:assert/strict');
const { test } = require('node:test');
const { readFileSync } = require('node:fs');
const ts = require('typescript');
require('@angular/compiler');
const { createEnvironmentInjector, runInInjectionContext } = require('@angular/core');
const { ActivatedRoute, Router, convertToParamMap } = require('@angular/router');
const { of } = require('rxjs');
const { HttpClient } = require('@angular/common/http');

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
const { activityDateRange } = require('../src/app/reports/activity-period.ts');
const { BehaviourReportsService } = require('../src/app/reports/behaviour-reports.service.ts');
const { HealthReportsService } = require('../src/app/reports/health-reports.service.ts');
const { ModuleRecordsService } = require('../src/app/module-records/module-records.service.ts');
const { BehaviourReportComponent } = require('../src/app/reports/behaviour-report.component.ts');
const { HealthReportComponent } = require('../src/app/reports/health-report.component.ts');
const { SupplementalReportComponent } = require('../src/app/reports/supplemental-report.component.ts');
const { ErpMealReportComponent } = require('../src/app/reports/erp-meal-report.component.ts');
const { ErpMealReportsService } = require('../src/app/reports/erp-meal-reports.service.ts');
const { BASE_PATH } = require('../src/app/api/variables.ts');

test('remaining report APIs preserve month/week across summaries, details, pagination and exports', () => {
  const calls = [];
  const injector = createEnvironmentInjector([
    { provide: HttpClient, useValue: { get: (url, options) => { calls.push({ url, params: options.params }); return of({}); } } },
    { provide: BASE_PATH, useValue: 'http://test' },
  ]);
  const activity = { month: 10, weekStart: '2026-10-26' };
  try {
    runInInjectionContext(injector, () => {
      const behaviours = new BehaviourReportsService();
      behaviours.summary('2026-2027', 'hk1', activity);
      behaviours.details('2026-2027', 'hk1', { dimension: 'all' }, 2, 20, activity);
      const health = new HealthReportsService();
      health.summary('diagnoses', 'month', '2026-2027', 'hk1', '', 2, 1000, true, activity);
      health.details({ periodStart: '2026-10-01', periodEnd: '2026-11-01' }, 2, 20, '2026-2027', 'hk1', activity);
      const records = new ModuleRecordsService();
      records.list('student-projects', 2, 1000, '', '2026-2027', 'hk1', true, activity);
      records.supplementalSummary('student-projects', 'status', '2026-2027', 'hk1', activity);
      new StudentsService().atRisk(2, 1000, '', '', '2026-2027', 'hk1', '', true, activity);
    });
    assert.equal(calls.length, 7);
    for (const { params } of calls) {
      assert.equal(params.get('month'), '10');
      assert.equal(params.get('weekStart'), '2026-10-26');
      assert.equal(params.get('academicYear'), '2026-2027');
      assert.equal(params.get('term'), 'hk1');
    }
  } finally { injector.destroy(); }
});

for (const kind of ['behaviour', 'health', 'mental', 'projects', 'activities', 'bus', 'meal-ordered-no-ticket', 'meal-no-order', 'meal-coffee-frequency']) {
  test(`${kind}: shared period changes reload the report and scope exports`, async () => {
    const calls = [];
    let exported;
    const page = { items: [{ key: 'student', name: 'Test', studentName: 'Test', values: {} }], totalCount: 1, totalPages: 1, page: 1, pageSize: 20, totalVisits: 1 };
    const health = kind === 'health';
    const behaviour = kind === 'behaviour';
    const meal = kind.startsWith('meal-');
    const Service = health ? HealthReportsService : behaviour ? BehaviourReportsService : meal ? ErpMealReportsService : ModuleRecordsService;
    const Component = health ? HealthReportComponent : behaviour ? BehaviourReportComponent : meal ? ErpMealReportComponent : SupplementalReportComponent;
    const injector = createEnvironmentInjector([
      { provide: LanguageService, useValue: { t: key => key, language: () => 'en' } },
      { provide: ToastService, useValue: { error: message => assert.fail(message) } },
      { provide: ActivatedRoute, useValue: { snapshot: { queryParamMap: convertToParamMap({}) } } },
      { provide: Router, useValue: {} },
      { provide: Service, useValue: {
        options: (...args) => { calls.push(args); return of({ grades: [], classes: [] }); },
        summary: (...args) => { calls.push(args); return of(behaviour ? { totalRecords: 1, studentCount: 1, byStudent: [] } : meal ? { ...page, totalEvents: 1 } : page); },
        details: (...args) => { calls.push(args); return of(page); },
        list: (...args) => { calls.push(args); return of(page); },
        supplementalSummary: (...args) => { calls.push(args); return of({ totalCount: 1, distinctStudents: 1, attentionCount: 1, distribution: [] }); },
      } },
      { provide: ReportExcelExportService, useValue: {
        collectPages: async fetch => { await fetch(1, 1000); return []; },
        download: async definition => { exported = definition; },
      } },
    ]);
    const component = runInInjectionContext(injector, () => new Component());
    try {
      component.academicYear = '2026-2027'; component.term = 'all';
      component.activityPeriod = { month: 10, weekStart: '2026-10-26' };
      if (health) component.report = 'diagnoses';
      else if (meal) component.report = kind.slice(5);
      else if (!behaviour) component.report = { moduleId: kind === 'mental' ? 'student-mental-health' : kind === 'bus' ? 'student-bus-attendance' : 'student-projects',
        primaryField: 'name', secondaryField: 'status', dimensionField: 'status', titleKey: 'Test', dimension: 'field', dateField: 'activity_date' };
      component.ngOnChanges({ academicYear: {}, activityPeriod: {} });
      assert.ok(calls.length > 0);
      if (meal) assert.deepEqual(component.effectiveRange(), { from: '2026-10-26', to: '2026-10-31' });
      else assert.ok(calls.some(args => args.some(value => value?.weekStart === '2026-10-26')));
      component.activityPeriod = { month: 11, weekStart: '2026-10-26' };
      component.ngOnChanges({ activityPeriod: {} });
      if (meal) assert.deepEqual(component.effectiveRange(), { from: '2026-11-01', to: '2026-11-01' });
      else assert.ok(calls.some(args => args.some(value => value?.month === 11)));
      await component.exportExcel();
      if (!meal) assert.ok(exported.filters.some(([label, value]) => label === 'reports.activityPeriod.week' && value === '26/10/2026 – 01/11/2026'));
      else assert.ok(exported.period.includes('2026-11-01'));
      if (meal) {
        calls.length = 0;
        component.term = 'hk2';
        component.ngOnChanges({ term: {} });
        assert.equal(component.validDates(), false);
        assert.equal(component.data(), null);
        assert.equal(calls.length, 0, 'a month outside the semester has no data and does not send an invalid API range');
      }
    } finally { component.ngOnDestroy?.(); injector.destroy(); }
  });
}

test('meal ranges intersect academic year, semester, month and week, including school-year boundaries', () => {
  assert.deepEqual(activityDateRange('2026-2027', 'all', { month: 9, weekStart: '2026-08-31' }), { from: '2026-09-01', to: '2026-09-06' });
  assert.deepEqual(activityDateRange('2026-2027', 'hk1', { month: 1, weekStart: '2027-01-18' }), { from: '2027-01-18', to: '2027-01-18' });
  assert.deepEqual(activityDateRange('2026-2027', 'hk2', { month: 1, weekStart: '2027-01-18' }), { from: '2027-01-19', to: '2027-01-24' });
});

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
    const available = component.reportGroups.flatMap(group => group.items.map(item => item.id));
    const eligible = [];
    for (const report of available) {
      component.activeReport.set(report);
      assert.equal(component.supportsActivityPeriod(), !['support-classes', 'school-current-course-score'].includes(report));
      if (component.supportsActivityPeriod()) eligible.push(report);
    }
    assert.equal(eligible.length, 29);
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
