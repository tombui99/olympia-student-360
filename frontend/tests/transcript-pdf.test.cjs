const assert = require('node:assert/strict');
const { test } = require('node:test');
const { readFileSync, writeFileSync, mkdirSync } = require('node:fs');
const { join } = require('node:path');
const ts = require('typescript');
const { PDFDocument, PDFArray, decodePDFRawStream } = require('pdf-lib');

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
    if (template.getPageCount() > 1) assert.deepEqual(pageStreams(pdf, pdf.getPages().at(-1)), pageStreams(template, template.getPage(1)));
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
