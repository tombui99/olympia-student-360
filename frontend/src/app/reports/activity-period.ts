export interface ActivityPeriodSelection {
  month: number | null;
  weekStart: string | null;
}

export function activityMonths(academicYear: string): { value: number; label: string }[] {
  const year = academicYearStart(academicYear);
  if (year === null) return [];
  return Array.from({ length: 12 }, (_, index) => {
    const month = (index + 8) % 12 + 1;
    return { value: month, label: `${String(month).padStart(2, '0')}/${month >= 9 ? year : year + 1}` };
  });
}

export function activityWeeks(academicYear: string, month: number | null = null): { value: string; label: string }[] {
  const year = academicYearStart(academicYear);
  if (year === null) return [];
  const yearStart = new Date(Date.UTC(year, 8, 1));
  const yearEnd = new Date(Date.UTC(year + 1, 8, 1));
  const rangeStart = month === null ? yearStart : new Date(Date.UTC(month >= 9 ? year : year + 1, month - 1, 1));
  const rangeEnd = month === null ? yearEnd : new Date(Date.UTC(month >= 9 ? year : year + 1, month, 1));
  const cursor = new Date(rangeStart);
  cursor.setUTCDate(cursor.getUTCDate() - (cursor.getUTCDay() + 6) % 7);
  const weeks: { value: string; label: string }[] = [];
  while (cursor < rangeEnd) {
    const start = cursor.toISOString().slice(0, 10);
    const end = new Date(cursor);
    end.setUTCDate(end.getUTCDate() + 6);
    weeks.push({ value: start, label: `${activityDateLabel(start)} – ${activityDateLabel(end.toISOString().slice(0, 10))}` });
    cursor.setUTCDate(cursor.getUTCDate() + 7);
  }
  return weeks;
}

export function normalizeActivityPeriod(academicYear: string, selection: ActivityPeriodSelection): ActivityPeriodSelection {
  const month = activityMonths(academicYear).some(option => option.value === selection.month) ? selection.month : null;
  const weekStart = month !== null && activityWeeks(academicYear, month).some(option => option.value === selection.weekStart) ? selection.weekStart : null;
  return { month, weekStart };
}

export function activityDateLabel(value: string): string {
  return value.split('-').reverse().join('/');
}

function academicYearStart(value: string): number | null {
  const match = /^(\d{4})-(\d{4})$/.exec(value);
  return match && Number(match[2]) === Number(match[1]) + 1 ? Number(match[1]) : null;
}
