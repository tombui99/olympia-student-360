const assert = require('node:assert/strict');
const { test } = require('node:test');
const { readFileSync, writeFileSync, mkdirSync } = require('node:fs');
const { join } = require('node:path');
const ts = require('typescript');
const { PDFDocument, PDFPage, PDFArray, decodePDFRawStream } = require('pdf-lib');

require.extensions['.ts'] = (loaded, filename) => loaded._compile(ts.transpileModule(readFileSync(filename, 'utf8'), {
  compilerOptions: { module: ts.ModuleKind.CommonJS, target: ts.ScriptTarget.ES2022, esModuleInterop: true },
}).outputText, filename);
const { buildStudentTranscriptPdf, TRANSCRIPT_LETTERHEADS } = require('../src/app/transcripts/transcript-pdf.ts');
const publicAssets = join(__dirname, '../public/assets');

const subjects = [
  ['math', 'Toán/Math'], ['physics', 'Vật lý/Physics'], ['chemistry', 'Hóa học/Chemistry'],
  ['biology', 'Sinh học/Biology'], ['literature', 'Ngữ văn/Literature'], ['history', 'Lịch sử/History'],
  ['geography', 'Địa lý/Geography'], ['english', 'Tiếng Anh/English'], ['ict', 'Tin học/ICT'],
  ['national-defense-education', 'Giáo dục quốc phòng và an ninh/National Defense Education'],
  ['economics-and-law', 'Kinh tế và Pháp luật/Economics and Law'], ['music', 'Âm nhạc/Music'],
  ['physical-education', 'Giáo dục thể chất/Physical Education'],
];

function fixture(ib = false) {
  return {
    studentProfileId: 7541, sisUserId: 'DEMO-7541', studentName: 'Nguyễn Minh An', dateOfBirth: '2008-02-21', gender: 'Nam',
    gradeLevel: '12', homeroom: '12H1', overallGpa: 3.64, overallAverage: 83.2,
    totalAcademicYears: 4, totalStudents: 1, totalSubjects: subjects.length, subjectOptions: [],
    years: [9, 10, 11, 12].map((grade, yearIndex) => {
      const entries = ib && grade >= 11 ? [subjects[0], subjects[4], subjects[7],
        ['computer-science', 'Khoa học máy tính/Computer Science'], ['business', 'Kinh doanh/Business'], subjects[8]] : subjects;
      return { academicYear: `${2022 + yearIndex}-${2023 + yearIndex}`, averageScore: 83.2, grades: [{
        gradeLevel: String(grade), averageScore: 83.2, gpa: 3.64, classOptions: [], subjectPage: 1,
        subjectPageSize: entries.length, totalSubjects: entries.length, totalSubjectPages: 1,
        subjects: entries.map(([subjectCode, subject], index) => ({
          subjectCode, subject, courseNames: [], classScores: [], semester1Final: 72 + index,
          semester2Final: 80 + index, yearAverage: 77 + index, credit: index >= 11 ? .5 : 1,
        })),
      }] };
    }),
  };
}

function assets(format) {
  const font = format === 'bilingual' ? 'carlito/Carlito' : 'nunito-sans/NunitoSans';
  return { letterhead: readFileSync(join(publicAssets, 'letterhead', TRANSCRIPT_LETTERHEADS[format])),
    regularFont: readFileSync(join(publicAssets, `fonts/${font}-Regular.ttf`)),
    boldFont: readFileSync(join(publicAssets, `fonts/${font}-Bold.ttf`)) };
}

function pageStreams(document, page) {
  const contents = page.node.Contents();
  const entries = contents instanceof PDFArray ? contents.asArray() : [contents];
  return entries.filter(Boolean).map(entry => document.context.lookup(entry)).map(stream =>
    Buffer.from(decodePDFRawStream(stream).decode()).toString('hex'));
}

for (const [format, expectedPages] of [['bilingual', 4], ['bilingual-gpa', 1], ['ib', 2], ['international', 2]]) {
  test(`${format}: uses original A4 letterhead, keeps every year and preserves programme pages`, async () => {
    const source = assets(format);
    const bytes = await buildStudentTranscriptPdf(fixture(format === 'ib'), { format, semester: 'all' }, source);
    const pdf = await PDFDocument.load(bytes);
    assert.equal(pdf.getPageCount(), expectedPages);
    assert.match(pdf.getTitle(), /Nguyễn Minh An/);
    for (const page of pdf.getPages()) {
      assert.equal(page.getWidth(), 595.32);
      assert.equal(page.getHeight(), 841.92);
    }
    const template = await PDFDocument.load(source.letterhead);
    if (template.getPageCount() > 1) {
      const retained = pageStreams(pdf, pdf.getPages().at(-1));
      const original = pageStreams(template, template.getPage(1));
      assert.ok(original.every(stream => retained.includes(stream)), 'original programme artwork and school information are retained');
    }
    if (process.env.OLYMPIA_PDF_PREVIEW_DIR) {
      mkdirSync(process.env.OLYMPIA_PDF_PREVIEW_DIR, { recursive: true });
      writeFileSync(join(process.env.OLYMPIA_PDF_PREVIEW_DIR, `${format}.pdf`), bytes);
    }
  });
}

test('long subjects and additional school years continue on branded pages instead of being dropped', async () => {
  const record = fixture();
  const grade = record.years[0].grades[0];
  grade.subjects = Array.from({ length: 45 }, (_, index) => ({ ...grade.subjects[0],
    subjectCode: `course-${index}`, subject: `Môn học/Extended interdisciplinary research and community development course ${index}` }));
  const options = { format: 'international', academicYears: [record.years[0].academicYear] };
  const expanded = await PDFDocument.load(await buildStudentTranscriptPdf(record, options, assets('international')));
  assert.ok(expanded.getPageCount() >= 3);
  const hidden = await PDFDocument.load(await buildStudentTranscriptPdf(record, {
    ...options, excludedSubjectCodes: grade.subjects.map(subject => subject.subjectCode),
  }, assets('international')));
  assert.equal(hidden.getPageCount(), 2);
});

test('empty year selection cannot silently create an incomplete transcript', async () => {
  await assert.rejects(buildStudentTranscriptPdf(fixture(), { format: 'international', academicYears: ['1900-1901'] },
    assets('international')), /No academic years selected/);
});

test('IB retains additional senior academic years on continuation pages', async () => {
  const record = fixture(true);
  record.years.push({ ...record.years[3], academicYear: '2026-2027' });
  const pdf = await PDFDocument.load(await buildStudentTranscriptPdf(record, { format: 'ib' }, assets('ib')));
  assert.equal(pdf.getPageCount(), 3);
});

async function rendered(record, options) {
  const calls = [];
  const original = PDFPage.prototype.drawText;
  PDFPage.prototype.drawText = function(text, options) {
    calls.push({ text, ...options, page: this });
    return original.call(this, text, options);
  };
  try { return { bytes: await buildStudentTranscriptPdf(record, options, assets(options.format)), calls }; }
  finally { PDFPage.prototype.drawText = original; }
}

test('international names, clean subject names and left-aligned semester/year cells reach the PDF', async () => {
  const record = fixture();
  record.studentName = 'Thái Vũ Minh Châu';
  record.years[0].grades[0].subjects[0].subject = '2526 Academic English/ Tiếng Anh học thuật 11E1.1';
  const { calls } = await rendered(record, { format: 'international', academicYears: [record.years[0].academicYear] });
  assert.ok(calls.some(call => call.text === 'Student name: Chau Vu Minh Thai'));
  assert.ok(calls.some(call => call.text === 'Academic English'));
  assert.ok(calls.every(call => !call.text.includes('11E1.1')));
  const s1 = calls.find(call => call.text === 'S1F');
  const firstMark = calls.find(call => call.text === 'B' && call.y < s1.y);
  assert.equal(firstMark.x, s1.x);
});

test('average rows follow academic-year defaults and saved per-year visibility', async () => {
  const record = fixture();
  let output = await rendered(record, { format: 'bilingual', academicYears: ['2025-2026'] });
  assert.ok(!output.calls.some(call => call.text.startsWith('ĐTB các môn')));
  record.years[3].showAverage = true;
  output = await rendered(record, { format: 'bilingual', academicYears: ['2025-2026'] });
  assert.ok(output.calls.some(call => call.text.startsWith('ĐTB các môn')));
  record.record = { revision: 1, years: [{ academicYear: '2025-2026', showAverage: true, credits: [], ibSubjects: [] }] };
  output = await rendered(record, { format: 'bilingual', academicYears: ['2025-2026'] });
  assert.ok(output.calls.some(call => call.text.startsWith('ĐTB các môn')));
});

test('IB marks, levels, predicted total, enrollment/graduation/exam dates and notes are exported', async () => {
  const record = fixture(true);
  record.record = { revision: 1, enrollmentDate: '2022-09-05', graduationDate: '2026-06-01',
    graduationExamDate: '2026-05-20', ibReleaseDate: '2026-07-06', notes: 'Official assessment information supplied by the school.', ibCorePoints: 2,
    years: [{ academicYear: '2025-2026', gradeLevel: '12', credits: [], ibSubjects:
      record.years[3].grades[0].subjects.map((subject, index) => ({ subjectCode: subject.subjectCode,
        subjectName: subject.subject.split('/').at(-1), level: index < 3 ? 'HL' : 'SL',
        predictedGrade: '6', term1: '4', term2: '5', term3: '6', term4: '7' })) }] };
  const { bytes, calls } = await rendered(record, { format: 'ib', academicYears: ['2025-2026'] });
  assert.ok(calls.some(call => call.text === 'HL'));
  assert.ok(calls.some(call => call.text === 'SL'));
  for (const mark of ['4', '5', '6', '7', '38']) assert.ok(calls.some(call => call.text === mark), `IB mark/total ${mark}`);
  for (const date of ['2022-09-05', '2026-06-01', '2026-05-20', '2026-07-06']) assert.ok(calls.some(call => call.text.includes(date)));
  assert.ok(calls.some(call => call.text.includes('Official assessment information')));
  assert.equal((await PDFDocument.load(bytes)).getPageCount(), 3, 'notes continue before the programme appendix');
  if (process.env.OLYMPIA_PDF_PREVIEW_DIR) writeFileSync(join(process.env.OLYMPIA_PDF_PREVIEW_DIR, 'ib-entered.pdf'), bytes);
});

test('bilingual activities are grouped and never printed as numeric marks', async () => {
  const record = fixture();
  const grade = record.years[3].grades[0];
  grade.subjects = ['soccer', 'basketball', 'guitar', 'choir', 'graphic-design'].map(code => ({
    subjectCode: code, subject: code, courseNames: [], classScores: [], credit: 1,
    semester1Final: 85, semester2Final: 90, yearAverage: 88,
  }));
  const { calls } = await rendered(record, { format: 'bilingual', academicYears: ['2025-2026'] });
  const text = calls.map(call => call.text).join(' ');
  for (const group of ['Physical Education', 'Music', 'Fine Arts']) assert.ok(text.includes(group));
  for (const code of ['soccer', 'basketball', 'guitar', 'choir', 'graphic-design']) assert.ok(!text.includes(code));
  assert.equal(calls.filter(call => call.text === 'Đạt/Passed').length, 9);
});

test('long Unicode names, subjects and notes stay within page boundaries', async () => {
  const record = fixture();
  record.record = { revision: 1, years: [], notes: 'Ghi chú về quá trình học tập và tốt nghiệp. '.repeat(40) };
  record.years[0].grades[0].subjects[0].subject = 'Nghiên cứu/Extended interdisciplinary research and community development with advanced academic communication';
  const { calls } = await rendered(record, { format: 'bilingual-gpa', academicYears: ['2022-2023'] });
  assert.ok(calls.some(call => call.text === 'Transcript notes'));
  for (const call of calls) {
    assert.ok(call.x >= 0 && call.y >= 0, `text must stay on page: ${call.text}`);
    assert.ok(call.x + call.font.widthOfTextAtSize(call.text, call.size) <= call.page.getWidth() + .1, `text right edge: ${call.text}`);
  }
});


test('maximum-length subject labels continue without colliding with GPA footers or IB totals', async () => {
  const record = fixture(true);
  const longName = 'W'.repeat(255);
  for (const year of record.years) for (const subject of year.grades[0].subjects) {
    subject.subject = longName; subject.englishName = longName;
  }
  for (const format of ['bilingual-gpa', 'ib']) {
    const { calls } = await rendered(record, { format });
    for (const call of calls.filter(call => /^W+$/.test(call.text))) {
      const top = call.page.getHeight() - call.y - call.size;
      if (format === 'bilingual-gpa') assert.ok(top + call.size < 591, 'subject stays above Academic Standing');
      else assert.ok(top < 412 || (top >= 473 && top + call.size <= 663), 'IB subject stays inside its assessment section');
    }
  }
});


test('a newer incomplete IB prediction set cannot reuse an older complete total', () => {
  const { ibPredictedTotal } = require('../src/app/transcripts/transcript-display.ts');
  const record = fixture(true);
  const entries = Array.from({ length: 6 }, (_, index) => ({ subjectCode: `ib-${index}`, subjectName: `Subject ${index}`, level: 'HL', predictedGrade: '6' }));
  record.record = { revision: 1, ibCorePoints: 2, years: [
    { academicYear: '2024-2025', ibSubjects: entries, credits: [] },
    { academicYear: '2025-2026', ibSubjects: [{ ...entries[0], predictedGrade: '7' }], credits: [] },
  ] };
  assert.equal(ibPredictedTotal(record, ['2024-2025']), 38);
  assert.equal(ibPredictedTotal(record, ['2024-2025', '2025-2026']), null);
});

test('academic GPA in a filtered export uses the selected year only', async () => {
  const record = fixture();
  record.years[0].grades[0].gpa = 2.5;
  const { calls } = await rendered(record, { format: 'international', academicYears: ['2022-2023'] });
  const standing = calls.find(call => call.text.startsWith('Academic GPA'));
  assert.ok(calls.some(call => call.text === '2.50' && call.y === standing.y));
  assert.ok(!calls.some(call => call.text === '3.64'));
});
