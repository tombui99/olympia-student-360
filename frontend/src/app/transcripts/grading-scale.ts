export type TranscriptScoreScale = 'percentage' | 'gpa' | 'letter';

interface GradeBand {
  minimum: number;
  letter: string;
  gpa: number;
}

// Olympia's transcript scale. Canvas scores are percentages, so a 95% score
// corresponds to 9.5/10, 90% to 9.0/10, and so on.
const GRADE_BANDS: GradeBand[] = [
  { minimum: 95, letter: 'A+', gpa: 4.2 },
  { minimum: 90, letter: 'A', gpa: 4.0 },
  { minimum: 80, letter: 'A-', gpa: 3.7 },
  { minimum: 75, letter: 'B+', gpa: 3.3 },
  { minimum: 70, letter: 'B', gpa: 3.0 },
  { minimum: 65, letter: 'B-', gpa: 2.7 },
  { minimum: 60, letter: 'C+', gpa: 2.3 },
  { minimum: 55, letter: 'C', gpa: 2.0 },
  { minimum: 50, letter: 'C-', gpa: 1.7 },
  { minimum: 45, letter: 'D+', gpa: 1.3 },
  { minimum: 40, letter: 'D', gpa: 1.0 },
  { minimum: Number.NEGATIVE_INFINITY, letter: 'D-', gpa: 0.7 },
];

export function formatTranscriptScore(value: number | null | undefined, scale: TranscriptScoreScale): string {
  if (value == null || !Number.isFinite(value)) {
    return '—';
  }

  if (scale === 'percentage') {
    return `${value.toFixed(2)}%`;
  }

  const band = GRADE_BANDS.find((item) => value >= item.minimum) ?? GRADE_BANDS[GRADE_BANDS.length - 1];
  return scale === 'letter' ? band.letter : band.gpa.toFixed(1);
}
