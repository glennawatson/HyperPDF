# U3D artwork and PDF/X reports

## PDF/X report (`PdfXReader`)

The report is read-only. It keeps the raw `GTS_PDFXVersion` claim and shows it beside the level it maps to.

- A claim with the wrong spelling is `Malformed`. An unknown `PDF/X-<digit>` claim is `Unknown`.
- Each finding has a rule name, pass or fail, a plain message and a reference to the PDF/X part or overview it comes from.
- Checks: header version, encryption, title, trapping key, output intent, page boxes, annotations, fonts, LZW, JavaScript, embedded files, external content, transfer curves and alternate images.
- Some requirements are listed as not checked, and some levels are reported as not checked. The report says so. It never calls a file valid because a rule was skipped.
- A saved edit that breaks a rule fails that rule. The claim stays visible.

Limit: the ISO 15930 text is paywalled. References name the part and the free overview, not clause numbers. Rules follow the overview.

## U3D artwork (`Media/U3d`, `Pdf3DPresenter`)

`U3dModelReader` is a bounded, managed ECMA-363 reader. `U3dRenderer.Render` draws a static preview on the CPU with a depth buffer and SIMD. `U3dRenderer.Collect` plus `SkiaU3dPainter` draws the same scene on a Skia canvas, which a GPU backs when one is available.

- Nothing runs. U3D metadata and PDF 3D scripts are recorded, never run. `Pdf3DPresentation.RequiresUserAction` is always true.
- The fallback text is the annotation description, a scene summary, and what was not shown.
- Saving unrelated edits keeps the U3D stream bytes and views.

Not applied: progressive resolution updates, point and line sets, JPEG textures, trim curves, independent knots, specular highlights and fog. Each is reported as a problem. The Skia path sorts triangles far to near and does not do perspective-correct interpolation; the CPU path is exact.
