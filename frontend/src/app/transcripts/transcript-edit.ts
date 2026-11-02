import type { CanDeactivateFn } from '@angular/router';
import { ibSubjects, subjectName } from './transcript-display';
import type { SchoolTranscript, TranscriptRecord } from './transcripts.service';

export function createTranscriptDraft(source: SchoolTranscript): TranscriptRecord {
  const draft = structuredClone(source.record || { revision: 0, years: [] });
  const years = new Set([...source.years.map(year => year.academicYear),
    ...(source.academicYearOptions || []), ...draft.years.map(year => year.academicYear)]);
  for (const academicYear of years) {
    let entry = draft.years.find(year => year.academicYear === academicYear);
    if (!entry) {
      entry = { academicYear, gradeLevel: null, showAverage: null, credits: [], ibSubjects: [] };
      draft.years.push(entry);
    }
    for (const subject of source.years.find(year => year.academicYear === academicYear)?.grades.flatMap(grade => grade.subjects) || []) {
      if (!entry.credits.some(credit => credit.subjectCode === subject.subjectCode))
        entry.credits.push({ subjectCode: subject.subjectCode, credit: subject.credit });
    }
    // These are editable placeholders, never a conversion from Canvas scores to IB marks.
    entry.ibSubjects = ibSubjects(source, academicYear).map(row => structuredClone(row));
  }
  draft.years.sort((a, b) => a.academicYear.localeCompare(b.academicYear));
  return draft;
}

export function prepareTranscriptRecord(draft: TranscriptRecord, source: SchoolTranscript): TranscriptRecord {
  const payload = structuredClone(draft);
  for (const key of ['enrollmentDate', 'graduationDate', 'graduationExamDate', 'ibReleaseDate'] as const)
    payload[key] ||= null;
  payload.ibCorePoints ??= null;
  for (const year of payload.years) {
    const subjects = source.years.find(row => row.academicYear === year.academicYear)?.grades.flatMap(grade => grade.subjects) || [];
    year.ibSubjects = year.ibSubjects.filter(row => {
      const original = subjects.find(subject => subject.subjectCode === row.subjectCode);
      return !original || row.subjectCode.startsWith('manual-') ||
        [row.level, row.predictedGrade, row.term1, row.term2, row.term3, row.term4].some(value => value?.trim()) ||
        row.subjectName.trim() !== subjectName(original);
    });
    if (year.ibSubjects.length && !year.gradeLevel)
      year.gradeLevel = source.years.find(row => row.academicYear === year.academicYear)?.grades[0]?.gradeLevel || null;
  }
  return payload;
}

export function transcriptRecordError(record: TranscriptRecord): string | null {
  if (record.years.some(year => year.ibSubjects.length && !['11', '12'].includes(year.gradeLevel || '')))
    return 'transcript.editor.gradeRequired';
  if (record.enrollmentDate && record.graduationDate && record.enrollmentDate > record.graduationDate ||
      record.ibCorePoints != null && (!Number.isInteger(record.ibCorePoints) || record.ibCorePoints < 0 || record.ibCorePoints > 3) ||
      record.years.some(year => year.credits.some(row => typeof row.credit !== 'number' || !Number.isFinite(row.credit) || row.credit < 0 || row.credit > 2) ||
        year.ibSubjects.some(row => !row.subjectName.trim() ||
          [row.predictedGrade, row.term1, row.term2, row.term3, row.term4]
            .some(mark => !!mark?.trim() && !/^(?:[1-7]|[A-E]|Pass|Fail)$/.test(mark)))))
    return 'transcript.editor.error';
  return null;
}

export const transcriptEditGuard: CanDeactivateFn<{ canLeaveTranscript(): boolean }> =
  component => component.canLeaveTranscript();
