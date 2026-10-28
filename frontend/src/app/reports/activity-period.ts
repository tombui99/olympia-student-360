import type { HttpParams } from '@angular/common/http';

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

export function appendActivityPeriod(params: HttpParams, selection?: ActivityPeriodSelection | null): HttpParams {
  if (selection?.month != null) params = params.set('month', selection.month);
  if (selection?.month != null && selection.weekStart) params = params.set('weekStart', selection.weekStart);
  return params;
}

export function activityPeriodExportFilters(academicYear: string, selection: ActivityPeriodSelection | null,
  t: (key: string) => string): Array<[string, string]> {
  return [
    [t('reports.learningReview.month'), activityMonths(academicYear).find(option => option.value === selection?.month)?.label ?? t('reports.learningReview.allMonths')],
    [t('reports.activityPeriod.week'), activityWeeks(academicYear, selection?.month ?? null).find(option => option.value === selection?.weekStart)?.label ?? t('reports.activityPeriod.allWeeks')],
  ];
}

/** Inclusive dates for the meal API, intersecting school year, semester, month, and week. */
export function activityDateRange(academicYear: string, term: string, selection: ActivityPeriodSelection): { from: string; to: string } {
  const year = academicYearStart(academicYear)!;
  let start = new Date(Date.UTC(year, 8, 1));
  let end = new Date(Date.UTC(year + 1, 8, 1));
  if (term === 'hk1') end = new Date(Date.UTC(year + 1, 0, 19));
  if (term === 'hk2') { start = new Date(Date.UTC(year + 1, 0, 19)); end = new Date(Date.UTC(year + 1, 5, 1)); }
  const { month, weekStart } = normalizeActivityPeriod(academicYear, selection);
  if (month !== null) {
    const monthStart = new Date(Date.UTC(month >= 9 ? year : year + 1, month - 1, 1));
    const monthEnd = new Date(Date.UTC(month >= 9 ? year : year + 1, month, 1));
    if (start < monthStart) start = monthStart;
    if (end > monthEnd) end = monthEnd;
  }
  if (weekStart) {
    const week = new Date(weekStart + 'T00:00:00Z');
    const weekEnd = new Date(week); weekEnd.setUTCDate(weekEnd.getUTCDate() + 7);
    if (start < week) start = week;
    if (end > weekEnd) end = weekEnd;
  }
  end.setUTCDate(end.getUTCDate() - 1);
  return { from: start.toISOString().slice(0, 10), to: end.toISOString().slice(0, 10) };
}

function academicYearStart(value: string): number | null {
  const match = /^(\d{4})-(\d{4})$/.exec(value);
  return match && Number(match[2]) === Number(match[1]) + 1 ? Number(match[1]) : null;
}
