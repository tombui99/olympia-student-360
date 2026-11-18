import type { SchoolTranscript, SchoolTranscriptGrade, SchoolTranscriptYear, TranscriptBilingualRecord, TranscriptIbSubject, TranscriptPeriod, TranscriptSubject } from './transcripts.service';

export function cleanSubjectName(value: string): string {
  value = value.trim();
  for (let index = 0; index < 2; index++) value = value
    .replace(/^(?:(?:19|20)\d{2}\s*[-–/]\s*(?:19|20)\d{2}|\d{4})(?:\s*[-_:]\s*|\s+)/, '')
    .replace(/^OIS\s*[-_:]?\s*/i, '');
  return value.split('/').map(part => part.trim()
      .replace(/\s+(?:(?:grade|class)\s+)?(?:1[0-2]|[1-9])[a-z]{1,3}\d{1,2}(?:\.\d+)?\s*$/i, '').trim()).join('/');
}

export function subjectName(subject: TranscriptSubject, bilingual = false): string {
  if (bilingual) return subject.vietnameseName && subject.englishName
    ? `${subject.vietnameseName}/ ${subject.englishName}` : cleanSubjectName(subject.subject);
  if (subject.englishName) return cleanSubjectName(subject.englishName);
  const parts = cleanSubjectName(subject.subject).split('/').map(part => part.trim()).filter(Boolean);
  const vietnamese = (value: string) => /[đĐ\u0300-\u036f]/.test(value.normalize('NFD'));
  return parts.length > 1 ? (vietnamese(parts.at(-1)!) && !vietnamese(parts[0]) ? parts[0] : parts.at(-1)!) : parts[0] || '';
}

export function internationalStudentName(record: SchoolTranscript): string {
  const override = record.record?.transcriptName?.trim() || record.transcriptName?.trim();
  if (override) return override;
  const parts = (record.studentName || '').trim().split(/\s+/).filter(Boolean);
  const ordered = parts.length > 1 ? [parts.at(-1)!, ...parts.slice(1, -1), parts[0]] : parts;
  return ordered.join(' ').normalize('NFD').replace(/[\u0300-\u036f]/g, '').replace(/đ/g, 'd').replace(/Đ/g, 'D');
}

export function formatBirthDate(value?: string | null, locale = 'en-GB'): string {
  // Legacy API responses include a time. The ISO calendar day is the SIS birth date.
  const day = /^(\d{4}-\d{2}-\d{2})(?:T|$)/.exec(value || '')?.[1];
  if (!day) return '';
  const date = new Date(`${day}T00:00:00Z`);
  if (!Number.isFinite(date.getTime()) || date.toISOString().slice(0, 10) !== day) return '';
  return new Intl.DateTimeFormat(locale, { dateStyle: 'long', timeZone: 'UTC' }).format(date);
}

export const TRANSCRIPT_PERIODS = ['semester1', 'semester2', 'fullYear'] as const;
export const BILINGUAL_SUBJECTS = [
  { field: 'experientialActivities', code: 'experiential-activities', vietnameseName: 'Hoạt động trải nghiệm, hướng nghiệp', englishName: 'Experiential and Career Guidance Activities' },
  { field: 'localEducation', code: 'local-education', vietnameseName: 'Giáo dục địa phương', englishName: 'Local Education' },
] as const;
export const BILINGUAL_EVALUATIONS = [
  { field: 'conduct', name: 'Kết quả rèn luyện/ Conduct Result' },
  { field: 'academic', name: 'Kết quả học tập/ Academic Result' },
] as const;

const GROUPS: Record<string, { code: string; name: string }> = {};
for (const [codes, code, name] of [
  [['physical-education', 'basketball', 'soccer', 'volleyball', 'gym'], 'physical-education', 'Thể dục/Physical Education'],
  [['music', 'piano', 'guitar', 'drums', 'traditional-musical-instruments', 'vocal', 'choir'], 'music', 'Âm nhạc/Music'],
  [['fine-arts', 'graphic-design'], 'fine-arts', 'Mĩ thuật/Fine Arts'],
] as [string[], string, string][]) for (const activity of codes) GROUPS[activity] = { code, name };

const GRADED_ACTIVITIES = new Set(['gym', 'fine-arts', 'choir']);

export function isPassFail(code: string, bilingual = true): boolean {
  return (!!GROUPS[code] && (bilingual || !GRADED_ACTIVITIES.has(code))) ||
    ['service-learning', 'cas'].includes(code) || BILINGUAL_SUBJECTS.some(subject => subject.code === code);
}

export function bilingualSubjects(subjects: readonly TranscriptSubject[], authored?: TranscriptBilingualRecord | null,
  excluded: readonly string[] = []): TranscriptSubject[] {
  const rows: TranscriptSubject[] = [], groups = new Map<string, TranscriptSubject[]>();
  for (const subject of subjects) {
    if (excluded.includes(subject.subjectCode.toLowerCase())) continue;
    const group = GROUPS[subject.subjectCode];
    if (!group) rows.push(subject);
    else groups.set(group.code, [...(groups.get(group.code) || []), subject]);
  }
  const result = (values: (number | null | undefined)[]) => {
    const available = values.filter((value): value is number => value != null && Number.isFinite(value));
    return available.length ? available.every(value => value >= 50) ? 100 : 0 : null;
  };
  for (const [code, entries] of groups) {
    const semester1Final = result(entries.map(row => row.semester1Final));
    const semester2Final = result(entries.map(row => row.semester2Final));
    const annual = entries.every(row => row.yearAverage != null) ? result(entries.map(row => row.yearAverage))
      : semester1Final != null && semester2Final != null ? result([semester1Final, semester2Final]) : null;
    rows.push({ ...entries[0], subjectCode: code, subject: GROUPS[code].name,
      englishName: GROUPS[code].name.split('/')[1], vietnameseName: GROUPS[code].name.split('/')[0],
      semester1Final, semester2Final, yearAverage: annual, credit: 1,
      courseNames: entries.flatMap(row => row.courseNames), classScores: entries.flatMap(row => row.classScores) });
  }
  for (const { field, code, vietnameseName, englishName } of BILINGUAL_SUBJECTS) {
    if (excluded.includes(code)) continue;
    const marks = authored?.[field];
    if (!marks || !TRANSCRIPT_PERIODS.some(period => marks[period])) continue;
    const index = rows.findIndex(row => row.subjectCode === code);
    const source = rows[index];
    const passScore = (period: TranscriptPeriod) => marks[period] === 'Pass' ? 100 : marks[period] === 'Fail' ? 0 : null;
    const row: TranscriptSubject = {
      ...(source || { subjectCode: code, subject: `${vietnameseName}/${englishName}`, englishName, vietnameseName,
        courseNames: [], classScores: [], credit: 0 }),
      semester1Final: source?.semester1Final ?? passScore('semester1'),
      semester2Final: source?.semester2Final ?? passScore('semester2'),
      yearAverage: source?.yearAverage ?? passScore('fullYear'),
    };
    if (index < 0) rows.push(row);
    else rows[index] = row;
  }
  return rows;
}

export function displayedGpa(grade: SchoolTranscriptGrade, showSemester1: boolean): { value: number | null; semester1: boolean } {
  if (grade.gpa != null) return { value: grade.gpa, semester1: false };
  if (showSemester1 && grade.semester1Gpa != null) return { value: grade.semester1Gpa, semester1: true };
  return { value: null, semester1: false };
}

export function showAverage(year: SchoolTranscriptYear, record?: SchoolTranscript): boolean {
  return record?.record?.years.find(row => row.academicYear === year.academicYear)?.showAverage
    ?? year.showAverage ?? Number(year.academicYear.slice(0, 4)) < 2024;
}

export function academicGpa(grades: readonly SchoolTranscriptGrade[]): number | null {
  const values = grades.filter(grade => Number(grade.gradeLevel) >= 9 && Number(grade.gradeLevel) <= 12 && grade.gpa != null);
  return values.length ? Math.round((values.reduce((sum, grade) => sum + grade.gpa!, 0) / values.length + Number.EPSILON) * 100) / 100 : null;
}

export function ibSubjects(record: SchoolTranscript, academicYear: string): TranscriptIbSubject[] {
  const saved = record.record?.years.find(year => year.academicYear === academicYear)?.ibSubjects || [];
  const known = record.years.find(year => year.academicYear === academicYear)?.grades
    .filter(grade => Number(grade.gradeLevel) >= 11).flatMap(grade => grade.subjects) || [];
  return [...saved, ...known.filter(subject => !saved.some(row => row.subjectCode === subject.subjectCode))
    .map(subject => ({ subjectCode: subject.subjectCode, subjectName: subjectName(subject) }))];
}

export function ibPredictionYear(record: SchoolTranscript, academicYears: readonly string[]): string | undefined {
  return [...academicYears].sort().reverse().find(year => ibSubjects(record, year).some(row => row.predictedGrade?.trim()));
}

export function ibPredictedTotal(record: SchoolTranscript, academicYears: readonly string[]): number | null {
  const year = ibPredictionYear(record, academicYears);
  if (!year) return null;
  const rows = ibSubjects(record, year).filter(row => /^[1-7]$/.test(row.predictedGrade || '') && row.level);
  return rows.length === 6 && record.record?.ibCorePoints != null
    ? rows.reduce((sum, row) => sum + Number(row.predictedGrade), record.record.ibCorePoints) : null;
}
