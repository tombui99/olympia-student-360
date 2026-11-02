# Student transcript PDFs

The transcript workspace opens student search by default. School summaries require selecting a year and grade; selection metadata comes from the catalog endpoint without reading marks. Score-scale controls default to Percentage, while PDF formats retain their prescribed grading scales. The IB editor and PDF support a separate graduation exam date alongside graduation and IB results-release dates. These are stored in the existing school-authored JSON record, independently of Canvas sync. The student transcript export menu downloads a PDF directly. The PDF renderer places searchable text on the school's original PDF pages from `frontend/public/assets/letterhead/`; it retains their vector artwork, watermarks, contact details and accreditation marks.

| Export | Supplied letterhead | Content layout |
| --- | --- | --- |
| Bilingual, 10-point | `bilingual-10-point.pdf` (supplied `01.1.`) | One academic year and grade per page, bilingual subject table, semester/year columns, evaluation rows and principal signature area |
| Bilingual, GPA | `bilingual-gpa.pdf` (supplied `02.1.`) | Two columns of academic-year tables, academic standing, grading scale and signature areas |
| IB | `ib.pdf` (supplied `03.1.`) | Programme introduction, predicted-grade and four-term assessment sections, Grade 9/10 tables and grading scale; original programme-information page appended |
| International, GPA | `international-gpa.pdf` (supplied `04.1.`) | Two columns of academic-year tables, academic standing and signature areas; original grading and school-information page appended |

The files use stable ASCII filenames, mapped in `transcript-pdf.ts`, to avoid Unicode filename differences between macOS, Git and Linux deployments. Each format uses its own supplied letterhead; artwork is never extracted from a completed student's transcript. The two-page letterheads retain programme/school information and artwork after all transcript and notes pages. The international appendix grading table is redrawn in its reserved area to correct cramped/aligned cells and include all scale bands. Only the crest's transparency is softened to match the printed references; text, logos and other artwork retain their original opacity.

Exports respect the selected academic years, semester filter and saved subject exclusions. The renderer also filters exclusions so a stale API response cannot put a hidden subject back into a PDF. Subject exclusions do not recalculate GPA or averages. Academic GPA on the student preview and PDF uses the selected academic years with valid Grade 9–12 annual GPAs. Canvas percentages are converted to a 10-point result for the numeric bilingual format and to the existing school letter-grade scale for GPA tables.

Authorized academic staff click **Edit transcript** (or **Edit IB transcript**) inside the transcript preview. Names and credits are edited in their table rows, historical grade/average visibility beside each year, and dates/notes in the footer. IB subject names, levels, predictions and four-term marks are edited directly in the IB assessment table; staff can add IB subjects/years and enter TOK/EE bonus points. There is no separate editor dialog. One Save/Cancel action covers the complete draft across formats and selected years. Canvas scores remain read-only; exports are disabled until the draft is saved or cancelled. Navigation protects the active draft, failed saves retain it, and cancelling a revision conflict refreshes the current record.

The backend stores official names, dates (including the separate graduation exam and IB release dates), notes, historical grade corrections, average-row visibility, per-subject credits/name overrides and IB entries in `StudentTranscriptRecords`, outside Canvas/SIS imports, with revision checks to prevent overwriting concurrent edits. Student access scope is enforced for reads and writes.

Normal editing shows the student's grade level directly. The historical-grade selector is hidden behind **Correct grade level** beside the grade heading and explains that it changes the school level, not marks. Existing saved corrections are preserved. Adding a manual IB year still requires choosing Grade 11 or 12.

IB marks come from these school-authored records. Canvas percentages are not converted into official IB marks. Missing official entries remain blank with an explanatory note. A predicted total requires six numeric 1–7 predictions with HL/SL levels plus confirmed 0–3 TOK/EE bonus points. A newer incomplete prediction set does not fall back to an older complete total. Evaluation results, signing dates, signatures and seals remain blank when no source is available. No names, marks, dates, signatures or seals are copied from completed reference transcripts.

For GPA/IB formats, student names default to given name + middle names + family name without Vietnamese accents, with a saved official-name override for compound names. The numeric bilingual format retains the Vietnamese full name. Formal transcripts show clean subject names, without raw Canvas course titles or class suffixes. School-authored English/Vietnamese subject-name overrides cover unmatched names.

Annual subject results require both semesters or an available completed annual total; Semester 1 alone is not an annual result. Live course totals are withheld from annual results until the course has ended (or, without course dates, the school-year June boundary has passed). Canvas aggregates require graded, non-excused, grade-bearing submission evidence in the grading period, or an explicit score override. Genuine graded zero marks are retained. Annual GPA requires complete academic subject results; pass/fail activities are excluded. Full-year subjects default to one credit, with editable credits for confirmed half-year courses. Historical grades use year-specific class/course evidence or a saved override, never a student's current grade as historical fallback.

The numeric bilingual average row is hidden by default from 2024–2025, with a saved per-student/per-year show/hide override. Sports, music and arts activities are consolidated into Physical Education, Music and Fine Arts only for the numeric bilingual presentation. Recorded activity results use the existing 50% pass threshold; any failing activity keeps the group's result as Not passed. Display grouping does not change raw Canvas records.

The GPA formats use separate measured column positions for the bilingual and international samples. IB secondary tables place annual GPA below the subject rows. GPA and IB text uses Nunito Sans to match the proportions of the samples; the numeric bilingual format uses Carlito. Both embed Vietnamese glyphs as font subsets and include their redistribution licenses under `public/assets/fonts/`.

Semester/year columns in GPA/IB secondary tables are left-aligned. Long subject names wrap and can continue across blocks without colliding with GPA footers or IB totals. Long notes use continuation pages before the original appendices. Longer lists continue on additional copies of the correct letterhead. Content is kept clear of fixed headers, footers, grading tables and signature areas. Exports continue to follow the selected academic years, so selecting one year produces fewer grade tables than the multi-year school examples.

PDF code loads only when an export is requested. Letterheads and fonts are cached for later exports; a failed asset request can be retried. Export errors show a localized message and leave the transcript usable.

Run the focused checks from `frontend/`:

```sh
npm run test:transcripts
```

To generate four previews with synthetic data for visual inspection:

```sh
OLYMPIA_PDF_PREVIEW_DIR=../tmp/transcript-pdfs npm run test:transcripts
```

Inspect every page after layout changes, including continuation pages and the retained IB/international appendices. The tests cover original page dimensions, original appendix content streams, all four export options, year/semester/exclusion forwarding, official-name formatting, saved IB marks/dates/totals/notes, average visibility, activity grouping, left alignment, Unicode names and continuation pages for long subject lists and maximum-length labels.
