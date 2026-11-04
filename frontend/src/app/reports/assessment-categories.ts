export const assessmentCategories = ['all', 'HS1', 'HS1.1', 'HS1.2', 'HS1.3', 'HS1.4', 'HS2', 'HS3', 'GPA'] as const;
export type AssessmentCategory = typeof assessmentCategories[number];
export function isAssessmentCategory(value: unknown): value is AssessmentCategory {
  return typeof value === 'string' && (assessmentCategories as readonly string[]).includes(value);
}

export function assessmentCategoryChoices(options: readonly string[] | undefined, selected: AssessmentCategory): AssessmentCategory[] {
  const available = new Set((options ?? ['all']).filter(isAssessmentCategory));
  // Keep a valid URL selection visible even if it has no data in the current period.
  available.add(selected);
  return assessmentCategories.filter(category => available.has(category));
}
