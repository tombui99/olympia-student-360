import { PDFDict, PDFDocument, PDFFont, PDFName, PDFNumber, PDFPage, rgb } from 'pdf-lib';
import fontkit from '@pdf-lib/fontkit';
import { formatTranscriptScore } from './grading-scale';
import type { SchoolTranscript, SchoolTranscriptGrade, TranscriptSubject } from './transcripts.service';

export type TranscriptPdfFormat = 'bilingual' | 'bilingual-gpa' | 'international' | 'ib';

export interface TranscriptPdfOptions {
  format: TranscriptPdfFormat;
  academicYears?: readonly string[];
  semester?: 'all' | 'semester1' | 'semester2';
  excludedSubjectCodes?: readonly string[];
}

export interface TranscriptPdfAssets {
  letterhead: Uint8Array;
  regularFont: Uint8Array;
  boldFont: Uint8Array;
}

// Stable ASCII filenames work consistently in Git checkouts and static deployments.
export const TRANSCRIPT_LETTERHEADS: Record<TranscriptPdfFormat, string> = {
  bilingual: 'bilingual-10-point.pdf',
  'bilingual-gpa': 'bilingual-gpa.pdf',
  ib: 'ib.pdf',
  international: 'international-gpa.pdf',
};

interface GradeRecord extends SchoolTranscriptGrade { academicYear: string; }
interface SubjectRow { subject: TranscriptSubject; lines: string[]; height: number; }
interface GradeBlock { grade: GradeRecord; rows: SubjectRow[]; continued: boolean; height: number; }
interface ColumnLayout { x: number; subjectWidth: number; centers: readonly number[]; }
interface GpaLayout { left: ColumnLayout; right: ColumnLayout; divider: number; ruleRight: number; }

// Coordinates measured against the corresponding completed school samples, in A4 points.
const GPA_LAYOUTS: Record<'bilingual-gpa' | 'international' | 'ib', GpaLayout> = {
  'bilingual-gpa': {
    left: { x: 31, subjectWidth: 135, centers: [150, 184, 218, 254] },
    right: { x: 317, subjectWidth: 133, centers: [148, 181, 215, 250] },
    divider: 307, ruleRight: 578,
  },
  international: {
    left: { x: 34, subjectWidth: 142, centers: [154, 188, 222, 256] },
    right: { x: 324, subjectWidth: 125, centers: [137, 170, 204, 237] },
    divider: 315, ruleRight: 578,
  },
  ib: {
    left: { x: 35, subjectWidth: 140, centers: [154, 190, 238, 264] },
    right: { x: 329, subjectWidth: 117, centers: [127, 158, 194, 231] },
    divider: 325, ruleRight: 570,
  },
};

const BLACK = rgb(.12, .12, .12);
const RULE = rgb(.35, .35, .35);
const PASS_FAIL = new Set(['music', 'physical-education', 'fine-arts', 'service-learning', 'cas']);
const GPA_ORDER = ['math', 'literature', 'physics', 'chemistry', 'biology', 'history', 'geography',
  'english', 'economics-and-law', 'ict', 'technology'];
const TEN_POINT_ORDER = ['math', 'physics', 'chemistry', 'biology', 'ict', 'literature', 'history',
  'geography', 'english', 'technology', 'national-defense-education', 'civic-education'];
const IB_ORDER = ['literature', 'vietnamese', 'english', 'computer-science', 'ict', 'business',
  'economics', 'biology', 'chemistry', 'physics', 'math', 'theory-of-knowledge', 'cas'];

export async function buildStudentTranscriptPdf(
  record: SchoolTranscript, options: TranscriptPdfOptions, assets: TranscriptPdfAssets,
): Promise<Uint8Array> {
  const document = await PDFDocument.create();
  const template = await PDFDocument.load(assets.letterhead);
  document.registerFontkit(fontkit);
  const [regular, bold] = await Promise.all([
    document.embedFont(assets.regularFont, { subset: true }),
    document.embedFont(assets.boldFont, { subset: true }),
  ]);
  document.setTitle(`${record.studentName || record.sisUserId || 'Student'} - School Transcript`);
  document.setAuthor('The Olympia Schools');
  document.setCreator('Olympia Student 360');
  const renderer = new TranscriptPdfRenderer(document, template, regular, bold, record, options);
  await renderer.render();
  // Retain the original programme/grading pages after all transcript content.
  for (const page of await document.copyPages(template, template.getPageIndices().slice(1))) {
    softenWatermark(page, options.format);
    document.addPage(page);
  }
  return document.save();
}

function softenWatermark(page: PDFPage, format: TranscriptPdfFormat): void {
  const resources = page.node.Resources();
  const forms = resources?.lookupMaybe(PDFName.of('XObject'), PDFDict);
  if (!forms?.has(PDFName.of('Fm0'))) return;
  // In the supplied letterheads Fm0 is the crest, painted with GS1. Keep its
  // original vector paths and reduce only its opacity to match the printed samples.
  const states = resources?.lookupMaybe(PDFName.of('ExtGState'), PDFDict);
  const watermark = states?.lookupMaybe(PDFName.of('GS1'), PDFDict);
  const opacity = format === 'bilingual-gpa' ? .02 : .055;
  watermark?.set(PDFName.of('ca'), PDFNumber.of(opacity));
  watermark?.set(PDFName.of('CA'), PDFNumber.of(opacity));
}

class TranscriptPdfRenderer {
  private readonly grades: GradeRecord[];
  private get gpaLayout(): GpaLayout {
    return GPA_LAYOUTS[this.options.format === 'bilingual-gpa' || this.options.format === 'ib'
      ? this.options.format : 'international'];
  }

  constructor(
    private readonly document: PDFDocument, private readonly template: PDFDocument,
    private readonly regular: PDFFont, private readonly bold: PDFFont,
    private readonly record: SchoolTranscript, private readonly options: TranscriptPdfOptions,
  ) {
    const excluded = new Set(options.excludedSubjectCodes?.map((code) => code.trim().toLowerCase()));
    const selectedYears = options.academicYears?.length ? new Set(options.academicYears) : null;
    this.grades = record.years.filter((year) => !selectedYears || selectedYears.has(year.academicYear))
      .flatMap((year) => year.grades.map((grade) => ({
        ...grade, academicYear: year.academicYear,
        subjects: grade.subjects.filter((subject) => !excluded.has(subject.subjectCode.toLowerCase())),
      })))
      .sort((a, b) => Number(a.academicYear.slice(0, 4)) - Number(b.academicYear.slice(0, 4)) ||
        Number(a.gradeLevel) - Number(b.gradeLevel));
    if (!this.grades.length) throw new Error('No academic years selected for export.');
  }

  async render(): Promise<void> {
    if (this.options.format === 'bilingual') await this.renderTenPoint();
    else if (this.options.format === 'ib') await this.renderIb();
    else await this.renderGpa(this.grades);
  }

  private async page(): Promise<PDFPage> {
    const [page] = await this.document.copyPages(this.template, [0]);
    softenWatermark(page, this.options.format);
    this.document.addPage(page);
    return page;
  }

  private text(page: PDFPage, value: string, x: number, top: number, size = 9, bold = false,
    width?: number, align: 'left' | 'center' | 'right' = 'left'): void {
    if (!value) return;
    const font = bold ? this.bold : this.regular;
    const clean = value.normalize('NFC').replace(/[\r\n\t]+/g, ' ');
    if (width) {
      const measured = font.widthOfTextAtSize(clean, size);
      if (measured > width) size *= width / measured;
      const padding = width - font.widthOfTextAtSize(clean, size);
      x += align === 'center' ? padding / 2 : align === 'right' ? padding : 0;
    }
    page.drawText(clean, { x, y: page.getHeight() - top - size, size, font, color: BLACK });
  }

  private line(page: PDFPage, x: number, top: number, endX: number, endTop = top, thickness = .45): void {
    page.drawLine({ start: { x, y: page.getHeight() - top },
      end: { x: endX, y: page.getHeight() - endTop }, thickness, color: RULE });
  }

  private wrap(value: string, width: number, size: number, bold = false): string[] {
    const font = bold ? this.bold : this.regular;
    const lines: string[] = [];
    let line = '';
    for (const word of value.normalize('NFC').split(/\s+/).filter(Boolean)) {
      const candidate = line ? `${line} ${word}` : word;
      if (font.widthOfTextAtSize(candidate, size) <= width) { line = candidate; continue; }
      if (line) lines.push(line);
      line = '';
      for (const letter of word) {
        if (line && font.widthOfTextAtSize(line + letter, size) > width) { lines.push(line); line = ''; }
        line += letter;
      }
    }
    if (line) lines.push(line);
    return lines.length ? lines : [''];
  }

  private paragraph(page: PDFPage, value: string, x: number, top: number, width: number, size = 8.3): number {
    const lines = this.wrap(value, width, size);
    lines.forEach((line, index) => this.text(page, line, x, top + index * (size + 2.6), size));
    return top + lines.length * (size + 2.6);
  }

  private metadata(page: PDFPage): void {
    const bilingual = this.options.format === 'bilingual-gpa';
    const genderX = this.options.format === 'ib' ? 482 : bilingual ? 445 : 460;
    this.text(page, `Student name: ${this.record.studentName || ''}`, bilingual ? 31 : 34, 108, 8.8, true, 225);
    this.text(page, `Birth Date: ${this.birthDate()}`, bilingual ? 276 : 262, 108, 8.8, true, bilingual ? 163 : 180);
    this.text(page, `${this.options.format === 'international' ? 'Sex' : 'Gender'}: ${this.gender(false)}`,
      genderX, 108, 8.8, true, 582 - genderX);
  }

  private birthDate(): string {
    if (!this.record.dateOfBirth) return '';
    const date = new Date(this.record.dateOfBirth);
    return Number.isFinite(date.getTime()) ? new Intl.DateTimeFormat('en-GB', {
      day: 'numeric', month: 'long', year: 'numeric', timeZone: 'UTC',
    }).format(date) : '';
  }

  private gender(bilingual: boolean): string {
    const value = this.record.gender?.trim() || '';
    if (/^(male|m|nam)$/i.test(value)) return bilingual ? 'Nam/ Male' : 'Male';
    if (/^(female|f|nữ|nu)$/i.test(value)) return bilingual ? 'Nữ/ Female' : 'Female';
    return value;
  }

  private subjectName(subject: TranscriptSubject, bilingual = false): string {
    return bilingual ? subject.subject : subject.subject.split('/').slice(1).join('/').trim() || subject.subject;
  }

  private orderedSubjects(grade: GradeRecord, bilingual = false): TranscriptSubject[] {
    const order = bilingual ? TEN_POINT_ORDER : this.options.format === 'ib' && Number(grade.gradeLevel) >= 11
      ? IB_ORDER : GPA_ORDER;
    const rank = (subject: TranscriptSubject) => { const index = order.indexOf(subject.subjectCode); return index < 0 ? 1000 : index; };
    return [...grade.subjects].sort((a, b) => rank(a) - rank(b));
  }

  private semesterValue(subject: TranscriptSubject, semester: 'semester1' | 'semester2'): number | null | undefined {
    if (this.options.semester && this.options.semester !== 'all' && this.options.semester !== semester) return null;
    return semester === 'semester1' ? subject.semester1Final : subject.semester2Final;
  }

  private score(value: number | null | undefined, subject?: TranscriptSubject, tenPoint = false): string {
    if (value == null || !Number.isFinite(value)) return '';
    if (subject && PASS_FAIL.has(subject.subjectCode)) return value >= 50
      ? tenPoint ? 'Đạt/Passed' : 'Pass' : tenPoint ? 'Chưa đạt/Not passed' : 'Fail';
    return tenPoint ? (value / 10).toFixed(1) : formatTranscriptScore(value, 'letter');
  }

  private gradeTitle(grade: GradeRecord): string {
    const number = Number(grade.gradeLevel);
    const suffix = number % 100 >= 11 && number % 100 <= 13 ? 'th' :
      number % 10 === 1 ? 'st' : number % 10 === 2 ? 'nd' : number % 10 === 3 ? 'rd' : 'th';
    return `${grade.gradeLevel}${suffix} Grade ${grade.academicYear}`;
  }

  private blocks(grades: GradeRecord[], maxHeight = 221, size = 9.2, gpaAtBottom = false): GradeBlock[] {
    const blocks: GradeBlock[] = [];
    for (const grade of grades) {
      const headingHeight = gpaAtBottom ? 37 : 25;
      let block: GradeBlock = { grade, rows: [], continued: false, height: headingHeight };
      for (const subject of this.orderedSubjects(grade)) {
        const subjectWidth = Math.min(this.gpaLayout.left.subjectWidth, this.gpaLayout.right.subjectWidth);
        const lines = this.wrap(this.subjectName(subject), subjectWidth, size);
        const row = { subject, lines, height: Math.max(gpaAtBottom ? 10.8 : 12.2, lines.length * (size + 1.7)) };
        if (block.rows.length && block.height + row.height > maxHeight) {
          blocks.push(block); block = { grade, rows: [], continued: true, height: headingHeight };
        }
        block.rows.push(row); block.height += row.height;
      }
      blocks.push(block);
    }
    return blocks;
  }

  private drawGpaBlock(page: PDFPage, block: GradeBlock, layout: ColumnLayout, top: number, size = 9.2,
    gpaAtBottom = false): void {
    const { x } = layout;
    const centers = layout.centers.map((offset) => x + offset);
    const title = this.gradeTitle(block.grade) + (block.continued ? ' (continued)' : '');
    this.text(page, title, x, top, size + .3, true, gpaAtBottom ? 250 : layout.centers[0] - 13);
    if (!gpaAtBottom) {
      this.text(page, 'GPA', centers[0] - 9, top, size + .3, true);
      this.text(page, block.grade.gpa?.toFixed(2) || '', centers[1] - 9, top, size + .3, true);
    }
    this.text(page, 'Subject', x, top + 12, size, true);
    ['S1F', 'S2F', 'YR', 'Credit'].forEach((label, index) =>
      this.text(page, label, centers[index] - 16, top + 12, size, true, 32, 'center'));
    let y = top + 25;
    for (const row of block.rows) {
      row.lines.forEach((line, index) => this.text(page, line, x, y + index * (size + 1.7), size));
      const values = [this.score(this.semesterValue(row.subject, 'semester1'), row.subject),
        this.score(this.semesterValue(row.subject, 'semester2'), row.subject), this.score(row.subject.yearAverage, row.subject),
        Number.isFinite(row.subject.credit) ? String(row.subject.credit) : ''];
      values.forEach((value, index) => this.text(page, value, centers[index] - 16, y, size, false, 32, 'center'));
      y += row.height;
    }
    if (gpaAtBottom) {
      this.text(page, 'GPA', centers[2] - 28, y + 2, size + .3, true);
      this.text(page, block.grade.gpa?.toFixed(2) || '', centers[3] - 24, y + 2, size + .3, true);
    }
  }

  private async renderGpa(grades: GradeRecord[]): Promise<void> {
    const blocks = this.blocks(grades);
    const layout = this.gpaLayout;
    for (let offset = 0; offset < blocks.length; offset += 4) {
      const page = await this.page();
      this.metadata(page);
      const visible = blocks.slice(offset, offset + 4);
      const firstHeight = Math.max(...visible.slice(0, 2).map((block) => block.height));
      const secondHeight = Math.max(0, ...visible.slice(2).map((block) => block.height));
      const pitch = Math.max(firstHeight + 18, Math.min(270, 451 - secondHeight));
      visible.forEach((block, index) => this.drawGpaBlock(page, block,
        index % 2 ? layout.right : layout.left, 136 + (index >= 2 ? pitch : 0)));
      if (visible.length > 1) this.line(page, layout.divider, 134, layout.divider, 589);
      this.line(page, 32, 591, layout.ruleRight);
      if (offset + 4 >= blocks.length) {
        this.text(page, 'Academic Standing', 34, 603, 9.5, true);
        const gradesWithGpa = this.record.years.flatMap((year) => year.grades)
          .map((grade) => Number(grade.gradeLevel)).filter((grade) => grade >= 9 && grade <= 12);
        const range = gradesWithGpa.length ? `G${Math.min(...gradesWithGpa)}-${Math.max(...gradesWithGpa)}` : 'G9-12';
        this.text(page, `Academic GPA (${range})`, 34, 617, 9.2);
        this.text(page, this.record.overallGpa?.toFixed(2) || '', 164, 617, 9.2, true);
        if (this.options.format === 'bilingual-gpa') {
          this.text(page, 'Graduation Date:', 34, 644, 9, true);
          this.line(page, 112, 655, 230);
          this.gradingScale(page, 677);
        }
        this.signatures(page);
      }
    }
  }

  private signatures(page: PDFPage): void {
    for (const [index, label] of ["School officer's name:", "Counselor's name:"].entries()) {
      const top = 744 + index * 25;
      this.text(page, label, 34, top, 8.5, true);
      this.line(page, 170, top + 11, 298);
      this.text(page, 'Signature:', 310, top, 8.5, true);
      this.line(page, 359, top + 11, 455);
      this.text(page, 'Date:', 464, top, 8.5, true);
      this.line(page, 491, top + 11, 570);
    }
  }

  private gradingScale(page: PDFPage, top: number, ib = false): void {
    this.text(page, ib ? 'Grade 9&10 Grading Scale' : 'Grading Scale', 34, top, 8.5, true);
    const rows = [
      ['10.0 GPA Scale', '9.5-10.0', '9.0-9.4', '8.0-8.9', '7.5-7.9', '7.0-7.4', '6.5-6.9', '6.0-6.4', '5.5-5.9', '5.0-5.4', '4.5-4.9', '4.0-4.4', ''],
      ['Letter Grade', 'A+', 'A', 'A-', 'B+', 'B', 'B-', 'C+', 'C', 'C-', 'D+', 'D', 'D-'],
      ['4.0 GPA Scale', '4.2', '4.0', '3.7', '3.3', '3.0', '2.7', '2.3', '2.0', '1.7', '1.3', '1.0', '0.7'],
    ];
    const y = top + 12, labelWidth = 105, cellWidth = (527 - labelWidth) / 12, rowHeight = 10.5;
    rows.forEach((row, index) => {
      this.text(page, row[0], 36, y + index * rowHeight + 1, 8);
      row.slice(1).forEach((value, col) => this.text(page, value, 34 + labelWidth + col * cellWidth,
        y + index * rowHeight + 1, 7.8, false, cellWidth, 'center'));
    });
    for (let index = 0; index <= 3; index++) this.line(page, 34, y + index * rowHeight, 561, undefined, .25);
    for (const x of [34, ...Array.from({ length: 13 }, (_, index) => 34 + labelWidth + index * cellWidth)])
      this.line(page, x, y, x, y + 3 * rowHeight, .25);
  }

  private async renderTenPoint(): Promise<void> {
    for (const grade of this.grades) {
      const chunks: SubjectRow[][] = [[]];
      let height = 0;
      for (const subject of this.orderedSubjects(grade, true)) {
        const lines = this.wrap(this.subjectName(subject, true), 195, 11);
        const row = { subject, lines, height: Math.max(20, lines.length * 12.2 + 4) };
        if (height + row.height > 282 && chunks[chunks.length - 1].length) { chunks.push([]); height = 0; }
        chunks[chunks.length - 1].push(row); height += row.height;
      }
      for (let index = 0; index < chunks.length; index++) {
        const page = await this.page();
        this.text(page, 'BẢNG ĐIỂM HỌC SINH/ SCHOOL TRANSCRIPT', 68, 126, 15.5, true, 459, 'center');
        if (index) this.text(page, '(tiếp theo/ continued)', 68, 148, 8, false, 459, 'center');
        this.text(page, `Họ và tên/ Full name: ${this.record.studentName || ''}`, 70, 180, 11.5, true, 310);
        this.text(page, `Ngày sinh/ Date of birth: ${this.birthDate()}`, 70, 201, 11.5, true, 310);
        this.text(page, `Giới tính/ Sex: ${this.gender(true)}`, 70, 222, 11.5, true, 310);
        this.text(page, `Năm/ Year: ${grade.academicYear}`, 375, 180, 11.5, true, 145, 'right');
        this.text(page, `Lớp/ Class: ${grade.gradeLevel}`, 375, 201, 11.5, true, 145, 'right');
        this.tenPointTable(page, grade, chunks[index], index === chunks.length - 1);
      }
    }
  }

  private tenPointTable(page: PDFPage, grade: GradeRecord, rows: SubjectRow[], last: boolean): void {
    const x = 68, top = 256, widths = [200, 85, 85, 85];
    const edges = [x]; widths.forEach((width) => edges.push(edges[edges.length - 1] + width));
    const headerHeight = 32;
    [['MÔN HỌC/ SUBJECT'], ['HỌC KỲ I/', '1st semester'], ['HỌC KỲ II/', '2nd semester'], ['CẢ NĂM/', 'Full year']]
      .forEach((lines, column) => lines.forEach((line, index) => this.text(page, line,
        edges[column] + 3, top + (column ? 3 + index * 12 : 10), 11, true, widths[column] - 6, column ? 'center' : 'left')));
    let y = top + headerHeight;
    this.line(page, x, y, edges[4]);
    for (const row of rows) {
      row.lines.forEach((line, index) => this.text(page, line, x + 3, y + 4 + index * 12.2, 11));
      [this.semesterValue(row.subject, 'semester1'), this.semesterValue(row.subject, 'semester2'), row.subject.yearAverage]
        .forEach((value, column) => this.text(page, this.score(value, row.subject, true), edges[column + 1] + 3,
          y + 4, 11, false, widths[column + 1] - 6, 'center'));
      y += row.height; this.line(page, x, y, edges[4]);
    }
    if (last) {
      this.text(page, 'ĐTB các môn/ Average Education', x + 3, y + 4, 11, true, 195);
      this.text(page, this.score(grade.averageScore, undefined, true), edges[3], y + 4, 11, true, 85, 'center');
      y += 20; this.line(page, x, y, edges[4]);
      const evaluationTop = y;
      this.text(page, 'XẾP LOẠI/ EVALUATION', x + 3, y + 4, 11, true);
      y += 20; this.line(page, x, y, edges[4]);
      for (const label of ['Kết quả rèn luyện/ Conduct Result', 'Kết quả học tập/ Academic Result']) {
        this.text(page, label, x + 3, y + 4, 11, false, 195);
        y += 20; this.line(page, x, y, edges[4]);
      }
      for (const edge of edges.slice(1, -1)) {
        this.line(page, edge, top, edge, evaluationTop);
        this.line(page, edge, evaluationTop + 20, edge, y);
      }
      const signatureTop = Math.max(673, y + 28);
      this.text(page, 'Date: ____________________', 310, signatureTop, 10);
      this.text(page, 'HIỆU TRƯỞNG/ PRINCIPAL', 310, signatureTop + 23, 11.5, true);
      this.line(page, 326, Math.min(780, signatureTop + 99), 479);
    } else for (const edge of edges.slice(1, -1)) this.line(page, edge, top, edge, y);
    this.line(page, x, top, edges[4], top, 1);
    this.line(page, x, top, x, y, 1);
    this.line(page, edges[4], top, edges[4], y, 1);
    this.line(page, x, y, edges[4], y, 1);
  }

  private async renderIb(): Promise<void> {
    const upper = this.grades.filter((grade) => Number(grade.gradeLevel) >= 11);
    const secondary = this.blocks(this.grades.filter((grade) => Number(grade.gradeLevel) < 11).reverse(), 190, 8.5, true);
    const assessments: { grades: GradeRecord[]; rows: SubjectRow[] }[] = [];
    for (let offset = 0; offset < Math.max(upper.length, 1); offset += 2) {
      const grades = upper.slice(offset, offset + 2);
      const unique = new Map<string, TranscriptSubject>();
      grades.forEach((grade) => this.orderedSubjects(grade).forEach((subject) => unique.set(subject.subjectCode, subject)));
      let assessment = { grades, rows: [] as SubjectRow[] }, height = 0;
      for (const subject of unique.values()) {
        const lines = this.wrap(this.subjectName(subject), 149, 8.6);
        const row = { subject, lines, height: Math.max(11, lines.length * 10.5) };
        if (height + row.height > 105 && assessment.rows.length) {
          assessments.push(assessment); assessment = { grades, rows: [] }; height = 0;
        }
        assessment.rows.push(row); height += row.height;
      }
      assessments.push(assessment);
    }
    const pages = Math.max(assessments.length, Math.ceil(secondary.length / 2), 1);
    for (let index = 0; index < pages; index++) {
      const page = await this.page(); this.metadata(page);
      this.text(page, 'International Baccalaureate Diploma Programme', 35, 129, 9.2, true);
      this.text(page, 'Enrolled at The Olympia Schools since: ____________________', 35, 144, 8.6);
      this.text(page, 'Final official IB grades release date: ____________________', 35, 155, 8.6);
      this.paragraph(page, 'The IB grades are assigned by the International Baccalaureate Organisation on a 7-point scale as follows:', 35, 168, 535, 8.6);
      this.text(page, '7 Exceptional    6 Excellent    5 Very Good    4 Good    3 Mediocre    2 Poor    1 Very Poor', 35, 181, 8.6, false, 535, 'center');
      this.paragraph(page, 'Additional points may be awarded for successful completion of the Theory of Knowledge (TOK) and the Extended Essay (EE), with a maximum of 3 points. A total of 24 points is needed to pass the IB Diploma.', 35, 194, 535, 8.6);
      this.paragraph(page, "University predicted grades are determined in all subjects to support students' university applications. University predicted grades are the teacher's prediction of the grade the student is expected to achieve in the subject, based on all the evidence of the student's work and the teacher's knowledge of IB standards.", 35, 221, 535, 8.6);
      this.ibAssessment(page, assessments[index]?.grades || [], assessments[index]?.rows || [], index > 0);
      const lower = secondary.slice(index * 2, index * 2 + 2);
      this.text(page, 'Secondary Education Assessment Grades', 35, 431, 9.2, true);
      this.paragraph(page, 'In Grades 9 and 10, students follow the secondary school curriculum established by the Ministry of Education and Training in Vietnam. Grades are evaluated using an internationally recognised system from A+ to D-, with clear criteria outlining the standards for each grade.', 35, 449, 535, 8.2);
      lower.forEach((block, column) => this.drawGpaBlock(page, block,
        column ? this.gpaLayout.right : this.gpaLayout.left, 473, 8.5, true));
      if (lower.length > 1) this.line(page, this.gpaLayout.divider, 473, this.gpaLayout.divider, 662);
      this.gradingScale(page, 684, true);
      this.line(page, 32, 734, 563);
      this.signatures(page);
    }
  }

  private ibAssessment(page: PDFPage, grades: GradeRecord[], rows: SubjectRow[], continued: boolean): void {
    this.text(page, 'University Predicted Grades' + (continued ? ' (continued)' : ''), 35, 259, 9.2, true, 249);
    this.text(page, 'School Assessment Grades', 294, 259, 9.2, true);
    this.text(page, 'Subject', 35, 282, 8.8, true);
    this.text(page, 'Level', 188, 282, 8.8, true);
    this.text(page, 'Predicted', 220, 282, 8.8, true);
    this.text(page, 'grade', 228, 293, 8.8, true);
    const years = grades.length ? grades : [null, null];
    years.forEach((grade, index) => {
      const x = 296 + index * 155, width = 132;
      this.text(page, grade ? this.gradeTitle(grade) : `${11 + index}th Grade`, x, 272, 8.5, true, width);
      for (let term = 0; term < 4; term++) this.text(page, `Term ${term + 1}`, x + term * width / 4, 285, 8, true, width / 4, 'center');
    });
    let top = 306;
    rows.forEach((row) => {
      row.lines.forEach((line, index) => this.text(page, line, 35, top + index * 10.5, 8.6));
      // HL/SL, predicted grades and four assessment terms are intentionally blank:
      // Canvas semester totals are not equivalent to any of these school fields.
      top += row.height;
    });
    this.text(page, 'Total points', 165, 412, 8.8, true);
    this.line(page, 290, 257, 290, 429);
  }
}
