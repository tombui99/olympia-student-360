# Transcript update handoff

Status: implemented and verified locally. The optimized API is running locally and the transcript evidence index has been applied to the local development database. No production deployment or student-record edits were performed.

Performance follow-up: see [Transcript performance and workflow](transcript-performance.md) for the student-first route, required year/grade summary scope, API guard, catalog endpoint and measured cold/cached timings.

## Changes

- Annual results no longer fall back to a single available semester. The existing `(S1F + 2 × S2F) / 3` rule is retained when both semesters exist. An available completed annual result can be used otherwise.
- Live Canvas course totals are withheld from annual results until the course ends; when course dates are absent, the existing school-calendar June boundary is used. Course aggregates require grade-bearing submission evidence or an explicit source score override. Graded zeros remain valid.
- Annual GPA requires completed academic subject results and omits pass/fail activities. Academic GPA in student previews and exports uses selected Grade 9–12 years with valid annual GPAs. Subject exclusions remain presentation filters and do not recalculate GPA.
- Full-year subjects default to 1 credit even with Semester 1 marks only. Staff can set confirmed half-year credits and other approved credit values in the editor.
- Historical grade comes from the class/course for that academic year, with a saved per-student/year correction available. Current SIS/Canvas student grade is not used as a historical fallback. Student birth year is never used to guess a historical grade.
- GPA and IB student names use the requested given/middle/family order without Vietnamese accents; the official-name override handles compound names and international records. Numeric bilingual transcripts retain the Vietnamese name.
- Formal subjects use clean names and language appropriate to the format. Academic English, Choir and Graphic Design have explicit mappings. Per-year subject-name overrides cover unmatched names. Source course names remain available in source-detail reporting.
- Numeric bilingual transcripts combine sports, music and fine-arts activities into the requested three reporting categories. They show pass/fail instead of numeric marks. The existing 50% threshold is retained; a failing activity keeps the combined group as Not passed.
- The numeric bilingual overall-average row is hidden by default from 2024–2025 and has saved per-student/year visibility controls on both preview and PDF.
- Subject settings support grade filtering and selecting/deselecting all matching subjects without changing exclusions outside the filter.
- A school-authored transcript editor saves names, dates, notes, historical grades, credit/name overrides and official IB assessment data separately from Canvas/SIS imports. Academic access and student scope are enforced, and revision checks prevent concurrent overwrites.
- The editor now runs directly in all four transcript previews. Subject names/credits appear as editable table cells, year settings beside the year, dates/notes in the footer, and official IB inputs in the IB table. The previous editor dialog was removed. Save/Cancel covers the whole draft, failed saves preserve entries, navigation protects active drafts and PDF export requires finishing edits first. Canvas score cells remain read-only.
- IB previews/PDFs print saved HL/SL, predictions and Term 1–4 results. Total predicted points require six numeric predictions with levels and confirmed TOK/EE bonus points. A newer incomplete prediction set cannot reuse an older complete total. No conversion of Canvas percentages to official IB marks is performed.
- The final client-feedback audit added a separate graduation exam date, persisted in the school-authored JSON record and printed on the IB preview/PDF. It is distinct from the graduation and IB results-release dates and requires no additional schema migration. Individual student is the first tab; score-scale controls default to Percentage without forcing Letter grade when switching GPA formats.
- All four formats have preview choices. GPA/IB secondary score columns are left-aligned. The international appendix grading table is repaired. Long names, notes and maximum-length subject labels wrap/continue without entering footer, total or signature areas.

## Verification

- Full backend regression suite: 135 passed, 0 skipped, using an isolated temporary MySQL instance. This included migration application/model consistency, transcript persistence, stale edits, student scope, score evidence, real zero marks and historical grades.
- Focused transcript backend checks were rerun after final rule/validation cleanup.
- The final exam-date follow-up passed all 21 focused backend and 23 frontend transcript checks, the production frontend build and the synthetic browser editor/save/IB-preview/PDF-download check. The updated IB PDF was rendered and visually inspected.
- Frontend transcript/PDF/settings/workflow checks: 23 passed.
- Inline-editor follow-up: 26 frontend transcript checks, the production build and a synthetic browser check covering all four formats, cancel without writes, unsaved navigation protection, invalid IB marks, concurrent-save conflicts, save/reload, manual IB years/subjects, hidden-year preservation, read-only access and PDF download.
- Frontend report checks: 48 passed.
- Frontend authentication resilience checks: 9 passed.
- Production Angular build passed using the bundled Node runtime and two build workers. Existing CommonJS optimization warnings for ExcelJS/fontkit dependencies remain. NuGet vulnerability-feed availability did not prevent backend compilation or tests.
- Headless Chrome smoke test with synthetic data verified preview selection, names, omitted raw class titles, editing and saving dates/notes/IB marks, reloaded IB results, grade filters, bulk selection and an actual PDF download, with no browser errors.
- Rendered all pages of the four baseline exports and checked the populated IB/notes example. Maximum-length labels are also covered by geometry checks in the PDF tests.

## Release steps and data follow-up

1. Deploy the backend migrations `20261102062329_AddStudentTranscriptRecords` and `20261102094148_IndexTranscriptAssessmentEvidence` using the repository's normal migration/deployment process, then deploy the matching frontend. The migration creates a separate transcript-record table; it does not rewrite source marks, names or student grades.
2. On the deployed system, check `anh.phan09`: Grade 11 in 2025–2026 and Grade 12 in 2026–2027 against the source records; the year without qualifying results must not acquire an annual GPA. Use the per-year grade correction if historical course/class evidence is absent or incorrect.
3. Check the current-year credits for Thái Vũ Minh Châu. Full-year courses should show 1. Preserve 0.5 only for an approved half-year course duration.
4. For `long.pd09`, enter/confirm the official IB subject names, levels, predictions, assessment terms and dates. The software can print these values; it cannot recover official IB results that have never been supplied. Inspect both pages/appendices and any notes continuation page after entry.
5. Confirm official transcript names for the examples and any compound-name exceptions. Enter approved English subject names for unmatched source courses.
6. If historical course marks become blank because the warehouse contains only an unsubstantiated Canvas aggregate, sync the grade-bearing submissions or confirm the appropriate explicit source override before issuing the transcript. Do not manufacture missing assessments.
7. Send the prepared Vietnamese responses only after deployment and the named-student checks. The IB response explicitly identifies the remaining school-data confirmation.

Client responses: [Vietnamese feedback-by-feedback draft](transcript-client-feedback-responses.vi.md).

Implementation and export behavior: [Student transcript PDFs](transcript-pdf-exports.md).
