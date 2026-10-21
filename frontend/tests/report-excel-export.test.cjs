const assert = require('node:assert/strict');
const { test } = require('node:test');
const { readFileSync, mkdirSync } = require('node:fs');
const { resolve } = require('node:path');
const ts = require('typescript');
require('@angular/compiler');
const { createEnvironmentInjector, runInInjectionContext } = require('@angular/core');
const { ActivatedRoute, Router } = require('@angular/router');
const { of } = require('rxjs');
const { Workbook } = require('exceljs');

require.extensions['.ts'] = (loaded, filename) => {
  loaded._compile(ts.transpileModule(readFileSync(filename, 'utf8'), {
    compilerOptions: {
      module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, experimentalDecorators: true,
    },
  }).outputText, filename);
};
const { createReportWorkbook } = require('../src/app/reports/report-excel-workbook.ts');
const { ReportExcelExportService, ReportExportLimitError } = require('../src/app/reports/report-excel-export.service.ts');
const { LeadershipReportComponent } = require('../src/app/reports/leadership-report.component.ts');
const { LeadershipReportsService } = require('../src/app/reports/leadership-reports.service.ts');
const { LanguageService } = require('../src/app/core/language/language.service.ts');
const { ToastService } = require('../src/app/core/toast/toast.service.ts');

const dictionaries = Object.fromEntries(['en', 'vi'].map(language => [language,
  JSON.parse(readFileSync(resolve(__dirname, `../public/assets/i18n/${language}.json`), 'utf8')),
]));
const languageService = language => ({ t: key => dictionaries[language][key] ?? key });
const detailRow = index => ({
  studentId: index + 1, sisUserId: String(index + 1).padStart(5, '0'), name: `Học sinh thử nghiệm ${index + 1}`,
  grade: '10', homeroom: '10T1', courseId: 100, courseName: '2627 Toán/Math 10QT',
  subaccountId: 20, subaccountName: 'Math/Toán', assessmentCategory: 'HS2', assignmentGroupName: 'GHK1',
  activityCount: 1, submitted: 0, missing: 0, late: 0, graded: 1, averageScorePercent: 75,
});

function createService(language = 'en') {
  const injector = createEnvironmentInjector([{ provide: LanguageService, useValue: languageService(language) }]);
  return { injector, service: runInInjectionContext(injector, () => new ReportExcelExportService()) };
}

function headerNumber(sheet, header) {
  for (let row = 1; row <= sheet.rowCount; row++) if (sheet.getCell(row, 1).value === header) return row;
  assert.fail(`Missing header ${header}`);
}

for (const language of ['en', 'vi']) {
  test(`${language}: distribution export is a styled XLSX with separate columns and numeric percentages`, async () => {
    const t = languageService(language).t;
    const definition = {
      fileName: 'distribution', title: t('reports.score.subjectDistribution.title'), period: '2026–2027 · Full year',
      filters: [[t('reports.score.schoolLevel'), 'Lower Secondary (Grades 5–9)'],
        [t('reports.leadership.subaccounts'), 'English/Tiếng Anh']],
      columns: [
        { header: t('reports.leadership.byCourse'), value: row => row.course },
        ...['A+', 'A', 'B', 'C', 'D'].map(header => ({ header, value: row => row[header], format: 'integer' })),
        { header: 'C+D share', value: row => row.share, format: 'percentage' },
        { header: 'Scored course results', value: row => row.total, format: 'integer' },
      ],
      rows: [{ course: '2627 Tiếng Anh/English ELA 5H1', 'A+': 1, A: 11, B: 4, C: 10, D: 1, share: 40.7, total: 27 },
        { course: '2627 Nhân văn/Humanities 9H1', 'A+': 5, A: 6, B: 2, C: 0, D: 0, share: 0, total: 13 }],
    };
    const original = createReportWorkbook(definition, t('reports.exportedAt'), new Date('2026-10-21T03:27:47Z'));
    const buffer = await original.xlsx.writeBuffer();
    assert.equal(Buffer.from(buffer).subarray(0, 2).toString(), 'PK');
    const workbook = new Workbook();
    await workbook.xlsx.load(buffer);
    const sheet = workbook.worksheets[0];
    const header = headerNumber(sheet, definition.columns[0].header);
    assert.equal(sheet.getCell('A3').value, definition.filters[0][0]);
    assert.equal(sheet.getCell('B3').value, definition.filters[0][1]);
    assert.equal(sheet.getCell('A1').font.bold, true);
    assert.equal(sheet.getCell(header, 1).fill.fgColor.argb, 'FF32124F');
    assert.equal(sheet.getCell(header, 1).font.color.argb, 'FFFFFFFF');
    assert.equal(sheet.getCell(header, 1).alignment.wrapText, true);
    assert.ok(sheet.getColumn(1).width >= 30);
    assert.ok(sheet.getColumn(8).width >= 20);
    assert.ok(sheet.getRow(header).height >= 32);
    assert.equal(sheet.getCell(header + 1, 1).value, definition.rows[0].course);
    assert.equal(sheet.getCell(header + 1, 2).value, 1);
    assert.equal(sheet.getCell(header + 1, 7).value, 0.40700000000000003);
    assert.equal(sheet.getCell(header + 1, 7).numFmt, '0.0%');
    assert.equal(sheet.getCell(header + 2, 7).value, 0);
    assert.equal(sheet.views[0].state, 'frozen');
    assert.equal(sheet.views[0].ySplit, header);
    assert.ok(sheet.autoFilter);
    assert.equal(sheet.rowCount, header + 2);
    if (process.env.REPORT_EXPORT_PREVIEW_DIR) {
      mkdirSync(process.env.REPORT_EXPORT_PREVIEW_DIR, { recursive: true });
      await original.xlsx.writeFile(resolve(process.env.REPORT_EXPORT_PREVIEW_DIR, `distribution-${language}.xlsx`));
    }
  });
}

test('XLSX preserves Vietnamese, leading-zero IDs, literal formula-like text, blanks and numeric zero', async () => {
  const workbook = createReportWorkbook({
    title: 'Report / details?', fileName: 'details',
    columns: [{ header: 'SIS ID', value: row => row.id, format: 'text' },
      { header: 'Name', value: row => row.name }, { header: 'Score', value: row => row.score, format: 'percentage' }],
    rows: [{ id: '00012', name: 'Nguyễn Minh Ngọc', score: 0 },
      { id: '=1+1', name: '+literal\nsecond line', score: null },
      { id: '00123', name: 'Đặng\u0001 An', score: NaN }],
  }, 'Exported at');
  const loaded = new Workbook();
  await loaded.xlsx.load(await workbook.xlsx.writeBuffer());
  const sheet = loaded.worksheets[0];
  const header = headerNumber(sheet, 'SIS ID');
  assert.equal(sheet.getCell(header + 1, 1).value, '00012');
  assert.equal(sheet.getCell(header + 1, 1).numFmt, '@');
  assert.equal(sheet.getCell(header + 1, 2).value, 'Nguyễn Minh Ngọc');
  assert.equal(sheet.getCell(header + 1, 3).value, 0);
  assert.equal(sheet.getCell(header + 2, 1).value, '=1+1');
  assert.equal(sheet.getCell(header + 2, 1).formula, undefined);
  assert.equal(sheet.getCell(header + 2, 3).value, null);
  assert.equal(sheet.getCell(header + 3, 2).value, 'Đặng An');
  assert.equal(sheet.getCell(header + 3, 3).value, null);
});

test('collection exports every filtered page and enforces the 25,000-row limit', async () => {
  const { injector, service } = createService();
  try {
    const source = Array.from({ length: 1501 }, (_, index) => index);
    const calls = [];
    const result = await service.collectPages(async (page, size) => {
      calls.push([page, size]);
      return { items: source.slice((page - 1) * size, page * size), totalCount: source.length, totalPages: 2 };
    });
    assert.deepEqual(result, source);
    assert.deepEqual(calls, [[1, 1000], [2, 1000]]);
    await assert.rejects(service.collectPages(async () => ({ items: [], totalCount: 25001, totalPages: 26 })), ReportExportLimitError);
  } finally { injector.destroy(); }
});

test('leadership detail export uses applied filters, all pages, screen labels and an async busy state', async () => {
  const source = Array.from({ length: 1501 }, (_, index) => detailRow(index));
  const calls = [];
  const toasts = [];
  const { injector: serviceInjector, service } = createService('vi');
  let exported;
  let finishDownload;
  const injector = createEnvironmentInjector([
    { provide: LanguageService, useValue: languageService('vi') },
    { provide: ToastService, useValue: { error: message => toasts.push(message) } },
    { provide: ReportExcelExportService, useValue: {
      collectPages: loader => service.collectPages(loader),
      download: definition => { exported = definition; return new Promise(resolve => { finishDownload = resolve; }); },
    } },
    { provide: LeadershipReportsService, useValue: { run: query => {
      calls.push(query);
      return of({ rows: source.slice((query.page - 1) * query.pageSize, query.page * query.pageSize),
        totalCount: source.length, pageSize: query.pageSize });
    } } },
    { provide: ActivatedRoute, useValue: {} }, { provide: Router, useValue: {} },
  ]);
  try {
    const component = runInInjectionContext(injector, () => new LeadershipReportComponent());
    component.reportKey = 'school-assessment-results';
    component.academicYear = '2026-2027';
    component.term = 'HK1';
    component.periodLabel = '2026–2027 · Học kỳ 1';
    component.appliedQuery = { reportKey: 'assessment-results', academicYear: '2026-2027', term: 'HK1',
      grade: '10', homeroom: '10T1', subaccountIds: [20], assessmentCategory: 'HS2', page: 1, pageSize: 20 };
    component.query = { ...component.appliedQuery, grade: '11', assessmentCategory: 'HS1' };
    component.options.set({ subaccounts: [{ id: 20, name: 'Math/Toán', accountIds: [20], gradeLevels: ['10'] }] });
    component.data.set({ totalCount: source.length, pageSize: 20, aggregateAvailable: true, rows: source.slice(0, 20) });
    const pending = component.exportExcel();
    await new Promise(setImmediate);
    assert.equal(component.exporting(), true);
    assert.equal(exported.rows.length, 1501);
    assert.equal(exported.rows.at(-1).sisUserId, '01501');
    assert.equal(calls.length, 2);
    assert.ok(calls.every(query => query.grade === '10' && query.homeroom === '10T1' && query.assessmentCategory === 'HS2'
      && query.subaccountIds[0] === 20 && query.export && query.includeSummary === false));
    const values = exported.columns.map(column => column.value(exported.rows[0]));
    assert.deepEqual(values.slice(0, 8), ['Học sinh thử nghiệm 1', '00001', '10', '10T1',
      '2627 Toán/Math 10QT', 'Math/Toán', dictionaries.vi['reports.leadership.category.HS2'], 'GHK1']);
    assert.equal(exported.columns.at(-1).format, 'percentage');
    assert.equal(values.at(-1), 75);
    assert.ok(exported.filters.some(([, value]) => value === '10T1'));
    await component.exportExcel();
    assert.equal(calls.length, 2);
    if (process.env.REPORT_EXPORT_PREVIEW_DIR) {
      const workbook = createReportWorkbook({ ...exported, rows: exported.rows.slice(0, 5) }, dictionaries.vi['reports.exportedAt']);
      await workbook.xlsx.writeFile(resolve(process.env.REPORT_EXPORT_PREVIEW_DIR, 'details-vi.xlsx'));
    }
    finishDownload();
    await pending;
    assert.equal(component.exporting(), false);
    assert.deepEqual(toasts, []);
  } finally { injector.destroy(); serviceInjector.destroy(); }
});

test('leadership export reports a workbook failure and clears its busy state', async () => {
  const errors = [];
  const injector = createEnvironmentInjector([
    { provide: LanguageService, useValue: languageService('en') },
    { provide: ToastService, useValue: { error: message => errors.push(message) } },
    { provide: ReportExcelExportService, useValue: {
      collectPages: async () => [detailRow(0)], download: async () => { throw new Error('write failed'); },
    } },
    { provide: LeadershipReportsService, useValue: {} },
    { provide: ActivatedRoute, useValue: {} }, { provide: Router, useValue: {} },
  ]);
  try {
    const component = runInInjectionContext(injector, () => new LeadershipReportComponent());
    component.appliedQuery = { academicYear: '2026-2027', term: 'HK1', subaccountIds: [], assessmentCategory: 'HS2' };
    component.data.set({ totalCount: 1 });
    await component.exportExcel();
    assert.equal(component.exporting(), false);
    assert.deepEqual(errors, [dictionaries.en['reports.exportError']]);
  } finally { injector.destroy(); }
});
