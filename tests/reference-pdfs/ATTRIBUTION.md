# Reference PDF sources and rights

CI downloads these unmodified PDFs by the URLs and SHA-256 hashes in [manifest.json](manifest.json). The repository does not contain their bytes. Rendering comparison artifacts include the source PDF, rendered images, and this notice.

| PDF ID | Source and credit | Reuse basis |
| --- | --- | --- |
| `pyhanko-ua-and-a` | [pyHanko test fixture](https://github.com/MatthiasValvekens/pyHanko/blob/871387373d318882e81a3fdd5e8d9089b6f5170e/internal/common-test-utils/src/pyhanko_testing_commons/test_data/data/pdf/minimal-pdf-ua-and-a.pdf), © 2020–2026 Matthias Valvekens. Its embedded Liberation Sans and Serif subsets identify version 2.1.5 and credit Google Corporation (2010) and Red Hat, Inc. (2012). | The fixture comes from pyHanko's [MIT-licensed repository](https://github.com/MatthiasValvekens/pyHanko/blob/871387373d318882e81a3fdd5e8d9089b6f5170e/LICENSE); its notice is reproduced below. The embedded fonts identify the [SIL Open Font License 1.1](https://github.com/liberationfonts/liberation-fonts/blob/main/LICENSE), which permits document embedding. |
| `loc-marbury-v-madison` | [Library of Congress PDF of *Marbury v Madison* (1803)](https://tile.loc.gov/storage-services/service/ll/usrep/usrep005/usrep005137/usrep005137.pdf). Credit: Law Library of Congress, United States Reports. | The Library of Congress describes its historical United States Reports digitisation as making [public-domain legal materials available](https://www.loc.gov/item/prn-18-026/historical-supreme-court-cases-now-online/2018-03-13/). |
| `irs-w9-2024` | [Form W-9 (March 2024)](https://www.irs.gov/pub/irs-prior/fw9--2024.pdf). Credit: U.S. Department of the Treasury, Internal Revenue Service. | An official U.S. federal government form. [17 USC § 105 (2024)](https://www.copyright.gov/title17/92chap1.html#105) excludes U.S. government works from U.S. copyright; the [IRS permits reproduction of official forms](https://www.irs.gov/irb/2005-50_IRB). |
| `usgs-morrison-1957` | [Morrison, Colorado historical topographic map (1957)](https://prd-tnm.s3.amazonaws.com/StagedProducts/Maps/HistoricalTopo/PDF/CO/24000/CO_Morrison_401624_1957_24000_geo.pdf). Credit: U.S. Geological Survey. | [USGS says its topographic maps are public domain](https://www.usgs.gov/faqs/are-usgs-topographic-maps-copyrighted), with limited exceptions for newer US Topo maps that do not apply to a 1957 map. USGS asks for source acknowledgement. |
| `usgs-lost-city-2023` | [Lost City, West Virginia and Virginia US Topo map (2023)](https://prd-tnm.s3.amazonaws.com/StagedProducts/Maps/USTopo/PDF/WV/WV_Lost_City_20230613_TM_geo.pdf). Credit: U.S. Geological Survey. | [USGS's rights statement](https://www.usgs.gov/faqs/are-usgs-topographic-maps-copyrighted) identifies commercial-data exceptions. This map's credit legend names NAIP imagery and U.S. Census Bureau roads, rather than those commercial sources. USGS asks for source acknowledgement. |
| `verapdf-pdfa*` (15 files) | PDF/A test files from the [veraPDF test file repository](https://github.com/veraPDF/veraPDF-corpus/tree/bb75f4f0073d9350dfd058c0162a367e6fadf25e), commit `bb75f4f`. Credit: veraPDF Consortium. Each manifest URL names the corpus path. | The corpus is licensed under [Creative Commons Attribution 4.0 International (CC BY 4.0)](https://creativecommons.org/licenses/by/4.0/). The files are used unmodified; the PDF/A tests write edited copies only to their own output folder. |
| `verapdf-ua-*` (3 files) | PDF/UA-1 tagged test files from the same [veraPDF test file repository](https://github.com/veraPDF/veraPDF-corpus/tree/bb75f4f0073d9350dfd058c0162a367e6fadf25e), commit `bb75f4f`. Credit: veraPDF Consortium. Their embedded Liberation Sans and Serif subsets are under the [SIL Open Font License 1.1](https://github.com/liberationfonts/liberation-fonts/blob/main/LICENSE). | [CC BY 4.0](https://creativecommons.org/licenses/by/4.0/), used unmodified. |
| `pdfjs-textfields`, `pdfjs-js-buttons`, `pdfjs-annotation-attachment` | Test files from the [pdf.js repository](https://github.com/mozilla/pdf.js/tree/87a106c03447d05ab8d759d5f9fa888310a7299e/test/pdfs), commit `87a106c`. Credit: Mozilla and the pdf.js contributors. Each was added by a pdf.js maintainer with the tests for its feature and records no outside source: `textfields.pdf` by Calixte Denizet ([3ca0360](https://github.com/mozilla/pdf.js/commit/3ca03603c2), 2022), `js-buttons.pdf` by Calixte Denizet ([7c3facb](https://github.com/mozilla/pdf.js/commit/7c3facb174), 2020) and `annotation-fileattachment.pdf` by Tim van der Meij, whose name is in its author metadata ([10902fd](https://github.com/mozilla/pdf.js/commit/10902fd882), 2016). `js-buttons.pdf` embeds Minion Pro subsets, as Acrobat embeds them; `annotation-fileattachment.pdf` embeds a Calibri subset, as Word embeds it. | Distributed in pdf.js under the [Apache License 2.0](https://github.com/mozilla/pdf.js/blob/87a106c03447d05ab8d759d5f9fa888310a7299e/LICENSE). Used unmodified. pdf.js test files taken from public bug reports are not listed here, because their reuse terms are not recorded. |
| `pypdf-*` (6 files) | Sample files from the [py-pdf sample files repository](https://github.com/py-pdf/sample-files/tree/02b8203bf416db3dbdb03438bc8278dfcb0555f3), commit `02b8203`. Credit: the py-pdf sample files contributors; `annotated_pdf.pdf` names Martin Thoma as its creator. Each manifest URL names the file. Embedded fonts: Computer Modern subsets in the LaTeX files; Liberation Sans and Serif, Ubuntu and OpenSymbol subsets in `libreoffice-form.pdf`, as LibreOffice embeds them; DejaVu Serif and Times New Roman subsets in `habibi.pdf`, as WeasyPrint embeds them. | The repository README states that every PDF in it is under [Creative Commons Attribution-ShareAlike 4.0 International (CC BY-SA 4.0)](https://creativecommons.org/licenses/by-sa/4.0/). The files are used unmodified, and tests write any changed copies only to their own output folders. Its optional-content map, `ocg_map_test.pdf`, is not listed here, because it credits Esri, TomTom, Garmin and OpenStreetMap map data under other terms. |

The PDF/A validation job runs these files and the library's own output through the [veraPDF](https://verapdf.org/) command-line validator, which CI downloads and runs as a separate program. veraPDF is dual-licensed under GPLv3+ and MPLv2+; it is not linked into or distributed with this project.

The rights statements above concern the source PDFs. They do not imply endorsement by any source organisation.

## pyHanko MIT notice

MIT License

Copyright (c) 2020-2026 Matthias Valvekens

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.
