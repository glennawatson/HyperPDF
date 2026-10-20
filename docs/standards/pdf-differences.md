# PDF Association differences audit

HyperPDF was checked against every case of [pdf-association/pdf-differences](https://github.com/pdf-association/pdf-differences) at commit `907fe96e52b73e491489eee545c47b119bf9989b`. The PDFs are CC BY 4.0 and the source is Apache-2.0 (see `tests/reference-pdfs/ATTRIBUTION.md`).

Method: each PDF was drawn by HyperPDF at a size big enough to judge, then compared by eye with the case's own explanation and its final correct picture. The pictures were not used as pixel oracles. PDFium was not used as the judge. Cases that say a result in words are checked by `PdfDifferencesCaseTests` (set `HYPERPDF_REFERENCE_PDF_DIR`, fetch the `pdfdiff-*` ids with `scripts/FetchReferencePdfs.cs`). Cases fixed in the same change as this audit have regression tests in `PdfDifferencesRenderTests`.

| Case | Clause | Expected | HyperPDF | Status |
| --- | --- | --- | --- | --- |
| Atomic-Fill+Stroke (3 files) | 11.7.4.4 | A combined fill and stroke is one object under transparency. | Matches the correct picture. | Matches |
| ColorBurn-ColorDodge (2) | 11.3.5 | Burn and dodge blend as in the pictures. | Matches. | Matches |
| Dashing-Degenerate | 8.5.3.2 | Dash and cap shapes of 44 and 45 high rectangles. | Matches. | Matches |
| Dashing-EndBeforeBend (2) | 8.4.3.6 | A dash that ends on a corner gets its end cap before the corner. | Looks the same as the correct picture. | Matches |
| DefaultColorSpaces (2) | 8.6.5 | Default RGB space applies to rg, RG, sc and SC, in forms and with inherited resources. | Matches both pictures. | Matches |
| IndexedColor | 8.6.6.3 | Out of range indices snap to the nearest valid index. | Both rows match. | Matches |
| Inline-Image-Abbreviations | 8.9.7 | When both keys are present the abbreviated key wins. | Full keys won, so tests 3 to 8 were wrong. | Fixed here |
| LargeMitreLimit (2) | 8.4.3.5 | Very large mitre limits are honoured. | Long mitre spikes are drawn; no correct picture to compare with. | Matches by the written rule |
| LineCap-Degenerate | 8.5.3.2 | A degenerate subpath paints a dot only with round caps. | Square caps also drew squares. | Fixed here |
| Negative-DashPhase (2) | 8.4.3.6 | A negative phase moves the pattern the way the picture shows. | Matches. | Matches |
| NegativeFontSize | 9.3 | Negative sizes mirror the text. | Drawing matches. Extracted text of the overlapping lines is interleaved. | Matches (drawing) |
| OverlappingGlyphClipping | 9.3.6 | Clip modes combine at the end of the text object. | Same clipping; the font differs from the picture's. | Accepted difference (font) |
| PageLabels-UX | 12.4.2 | Each page's label is the one it names. | All 16 pages agree, including the Eastern Arabic digits. | Matches, tested |
| PDF-version (3) | 7.5.2 | The version is 1.6 for each file. | 1.6. | Matches, tested |
| TextClipModeChanges | 9.3.6 | The clip changes only at ET. | Looks right; the case has no picture to compare with. | Not confirmed |
| Type3WordSpacing | 9.6.4 | A glyph that gives only a shape strokes with the stroke colour. Tw applies to spaces. | Stroke used the fill colour. | Fixed here |
| UnknownFilter (9 files) | 7.4 | Files with an unknown filter on an optional stream still open; text is kept. | All open and draw; text is kept where the page does not need the stream. | Matches, tested |
| UnknownFilter, image stream | 7.4 | A reader should not draw noise for an image it cannot decode. | Noise on black is drawn. | Follow-up |
| UnknownFilter, real-world pair | 7.4 | The README keeps the exact fault secret; a reader should show a placeholder or an error, not a different image. | Draws the page (letterhead, logos, QR code); the hidden fault was not found. | Not judged |
| VerticalText | 9.7 | Brackets, punctuation and the long vowel mark use their vertical forms. | Horizontal forms are used. | Follow-up |

Follow-up work is filed as separate issues. The viewer also does not yet tell the reader in words when a page uses a filter it cannot read.
