import type { SchoolTranscript, SchoolTranscriptGrade, SchoolTranscriptYear, TranscriptIbSubject, TranscriptSubject } from './transcripts.service';

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

const GROUPS: Record<string, { code: string; name: string }> = {};
for (const [codes, code, name] of [
  [['physical-education', 'basketball', 'soccer', 'volleyball', 'gym'], 'physical-education', 'Thể dục/Physical Education'],
  [['music', 'piano', 'guitar', 'drums', 'traditional-musical-instruments', 'vocal', 'choir'], 'music', 'Âm nhạc/Music'],
  [['fine-arts', 'graphic-design'], 'fine-arts', 'Mĩ thuật/Fine Arts'],
] as [string[], string, string][]) for (const activity of codes) GROUPS[activity] = { code, name };

export function isPassFail(code: string): boolean {
  return !!GROUPS[code] || ['service-learning', 'cas'].includes(code);
}

export function bilingualSubjects(subjects: readonly TranscriptSubject[]): TranscriptSubject[] {
  const rows: TranscriptSubject[] = [], groups = new Map<string, TranscriptSubject[]>();
  for (const subject of subjects) {
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
  return rows;
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
