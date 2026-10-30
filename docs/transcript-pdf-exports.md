# Student transcript PDFs

The student transcript export menu downloads a PDF directly. The PDF renderer places searchable text on the school's original PDF pages from `frontend/public/assets/letterhead/`; it retains their vector artwork, watermarks, contact details and accreditation marks.

| Export | Supplied letterhead | Content layout |
| --- | --- | --- |
| Bilingual, 10-point | `bilingual-10-point.pdf` (supplied `01.1.`) | One academic year and grade per page, bilingual subject table, semester/year columns, evaluation rows and principal signature area |
| Bilingual, GPA | `bilingual-gpa.pdf` (supplied `02.1.`) | Two columns of academic-year tables, academic standing, grading scale and signature areas |
| IB | `ib.pdf` (supplied `03.1.`) | Programme introduction, predicted-grade and four-term assessment sections, Grade 9/10 tables and grading scale; original programme-information page appended |
| International, GPA | `international-gpa.pdf` (supplied `04.1.`) | Two columns of academic-year tables, academic standing and signature areas; original grading and school-information page appended |

The files use stable ASCII filenames, mapped in `transcript-pdf.ts`, to avoid Unicode filename differences between macOS, Git and Linux deployments. Each format uses its own supplied letterhead; artwork is never extracted from a completed student's transcript. The two-page letterheads keep their original programme/grading text and artwork after all transcript content pages. Only the crest's transparency is softened to match the printed references; text, logos and other artwork retain their original opacity.

Exports respect the selected academic years, semester filter and saved subject exclusions. The renderer also filters exclusions so a stale API response cannot put a hidden subject back into a PDF. GPA and recorded averages retain their original values. Canvas percentages are converted to a 10-point result for the numeric bilingual format and to the existing school letter-grade scale for GPA tables.

IB levels, predicted grades, total predicted points and four-term assessments remain blank because semester totals do not provide these values. Evaluation results, graduation dates, official names, signing dates, signatures and seals are also left blank when no source is available. Semester averages are not inferred from a filtered subject list. No names, marks, dates, signatures or seals are copied from the completed reference transcripts.

The GPA formats use separate measured column positions for the bilingual and international samples. IB secondary tables place annual GPA below the subject rows. GPA and IB text uses Nunito Sans to match the proportions of the samples; the numeric bilingual format uses Carlito. Both embed Vietnamese glyphs as font subsets and include their redistribution licenses under `public/assets/fonts/`.

Long subject names wrap. Longer lists continue on additional copies of the correct letterhead. Content is kept clear of fixed headers, footers, grading tables and signature areas. Exports continue to follow the selected academic years, so selecting one year produces fewer grade tables than the multi-year school examples.

PDF code loads only when an export is requested. Letterheads and fonts are cached for later exports; a failed asset request can be retried. Export errors show a localized message and leave the transcript usable.

Run the focused checks from `frontend/`:

```sh
npm run test:transcripts
```

To generate four previews with synthetic data for visual inspection:

```sh
OLYMPIA_PDF_PREVIEW_DIR=../tmp/transcript-pdfs npm run test:transcripts
```

Inspect every page after layout changes, including continuation pages and the retained IB/international appendices. The tests cover original page dimensions, original appendix content streams, all four export options, year/semester/exclusion forwarding, Unicode names, and continuation pages for long subject lists.
