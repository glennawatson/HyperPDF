# Span, SIMD, pooling and GPU opportunities

A read-only pass over all production code (14 reviewers, one per area) found the items below. Each is a candidate, not a
measured result: build a BenchmarkDotNet case first, change the code, then compare. GPU offload was judged not worth it
anywhere except an opt-in Skia `GRContext` for very large rasters; readback costs more than the per-pixel work saves.

## Done (measured, short job, net11.0)

| Change | Before | After |
|---|---|---|
| Form finder luminance shares the vectorised OCR grey pass | 6.05 ms | 1.05 ms |
| Stencil row expansion by an 8-pixel table | 4.93 us | 0.27 us |
| Clean hex string decode with `Convert.FromHexString` | 742 ns | 76 ns |
| Search fold of ASCII text with one non-ASCII character | 137 us | 40 us |
| Alpha premultiply skips opaque 64-pixel blocks | 879 us | 130 us |
| PNG decode, 8-bit not interlaced, straight into the picture array | 11.8 ms | 3.7 ms |
| Linear barcodes (pooled modules, no lists) | 0.66 to 1.13 us | 0.24 to 0.43 us |
| QR encode (mask on a copy, packed 128-bit penalty scoring) | 184 us | 70 us |

Also done without a separate number: no bidi work for lines below U+0590, `F` operator prefilter in the PDF 2.0
conversion, plain literal strings shown without a copy, fewer list allocations in paragraph flow and PDF/A stream
filters, form field hit test checks bounds first.

## Best remaining candidates

1. Content: TJ arrays are tokenised twice (`ContentReader.ReadComposite` then `ContentText.ShowArrayItems`).
2. Content: form XObjects are re-inflated on every `Do`; a byte-bounded cache of decoded content would help repeated forms.
3. Filters: `PredictorFilter` Paeth and Sub decode are scalar and branchy; split head and body, vectorise Sub and Average.
4. JPEG colour conversion (`JpegColor.YccToRgb`) and chroma upsampling are scalar per pixel; reuse each upsampled row for 4:2:0.
5. ICC LUT and matrix row conversion has no colour cache (`IccLutTransform`, `IccMatrixTransform`).
6. Image alpha: `UpscaleBgra` and `ResamplePlane` recompute column maps per row; `Multiply` could be wider than 64 pixels.
7. Redaction: `ImageRedactor.PixelPlan.Blank` transforms every pixel; for unrotated images clear whole rows. `ToUnicodePruner` re-parses every page.
8. Optimizer: `DuplicateFinder` hashes every stream (bucket by length first); `InferredTagger` deflates a throwaway probe at the smallest level; `ImagePixels.BoxReduce` is scalar.
9. Signing and security: `PdfCmsVerifier` copies the whole signed range per signature; `Md5` rounds are a loop; RC4 rebuilds state with `%`.
10. Writing: stream recompression is serial and could be parallel with deterministic output; `PdfDocumentChecker` inflates content twice.
11. PDF/A: nine passes each walk the whole object graph (`PdfObjectGraph.Collect`); CID range lookups are linear.
12. Fonts and text: `TextFolding` and `TextHitTesting` could use struct-of-arrays boxes; `PdfCompositeFont.ToCid` could be paged-cached for CJK maps; `EmbeddedGlyphUse` locks per glyph.
13. UI: pointer hover runs four engine queries per move (cache the last answer by character box); annotation lists are re-fetched per frame for comment badges; search results build up to three strings per hit.
14. Core: `PdfRepair.Rebuild` scans byte by byte (use `IndexOf("obj")`); `PdfReader` object-stream lookups are O(index).
15. Skia heads: tone filtering renders each toned GPU tile twice; `GpuPreparedRenderSurface.GetOrCreateImage` locks per tile per frame; video frames allocate 8 MB when row strides differ.
16. Speech: Melo GRU input products can be precomputed; `TextNormalizer` runs about 26 regex passes without a cheap gate.
17. Image file codecs: TIFF and BMP decode follow the old PNG shape (copy, widen, convert); BMP and PostScript writers write per sample.

## Second pass (four reviewers, read-only, nothing measured yet)

Best first within each area. Build a benchmark before each change.

**Filters, graphics, content, rendering**

1. `PngPredictorEncoder.FilterSub/Up/Average/Paeth/Cost`: the encoder reads only the original row, so every filter and the cost sum can be vector code.
2. `ImageAlpha.MultiplyBlock`: widen BGRA to 16-bit lanes with the exact `Divide255` rounding; `ResamplePlane` should copy when the sizes match.
3. `ContentXObjects.DrawInlineImage` allocates `length + 4` bytes per inline image; `InlineImageSize` lexes the dictionary twice before a third parse.
4. `AsciiHexFilter.DecodeScalarChunk`: line-wrapped hex never takes the vector path; `Ascii85Filter` takes a span per group of five.
5. `SampleUnpacker.UnpackGeneric`: table-driven for 1, 2 and 4 bit samples; `JpegPixelConverter` chroma reduce and colour convert; `JpxSampleWriter` sYCC and unpremultiply.
6. `ContentText.ShowBytes`: compute the text matrix once per show, cache widths per code; `PdfTransfer` skip identity tables; `BuildTransfer` cache by function.
7. `SkiaRenderBackend.CreateImage` copies unpinned pixels each time; `Jbig2Composer.ApplyShifted`, `LzwFilter` refill, `CcittFaxDecoder` reference search.

**Fonts, text, syntax, structure**

1. `PdfParser.ParseNumberOrReference`: peek the next byte before lexing two more tokens, so number arrays stop paying for reference checks.
2. `XrefReader` entries: specialise the common widths with `BinaryPrimitives`; `ObjectStreamIndex.Read` parse plain digits directly.
3. `PdfLexer.SkipWhitespace/ReadRegular`: test one or two bytes before the vector call for short tokens.
4. `PdfTextPage` builds blocks and bounds eagerly; `TextPageBuild` walks the characters five times.
5. `CidMetricsTable.Fill`, `TrueTypeLocations.TryRead`, `CffCharsets`: bulk `Fill` and `ReverseEndianness` over spans.
6. `PdfText` PDFDoc and UTF-16BE conversions: ASCII fast path; `TextLinkParser`: one `IndexOfAny(':', '@')` gate per page.
7. `PdfPaginationProfile.CountMargins`: span keys instead of two strings per line; `XrefRepair.ScanObjects` repeats the `obj` search.

**Writing, security, conformance, creation, image files**

1. `PdfTextWrapper`, `BreakOverlong` and `LineBreaker.TrySplitWord` re-measure the whole line for every word (quadratic); keep a running width where fonts allow.
2. `AppearanceFontMetrics.Encode/MeasureAdvance` and `PdfCanvasFont.MeasurePrimary`: fused measure over a per-font advance table, no rented buffer.
3. `AtomBuilder.AddText`: slice words with `SearchValues`, share the space string.
4. `BilevelEncoder.Pack/Unpack` and `BilevelRecoder`: vector compare plus mask, and an 8-pixel table.
5. `PdfSecurityHandler.EncryptAes` makes two arrays; `DecryptAes` decrypts twice on bad padding.
6. `PngPixels`, `BmpPixels`, `TiffStrips`, `RasterSamples.FromBilevel`, `TgaRasterWriter`, `PdfPngWriter`: depth tables, bit-reverse table, one write per row, pooled rows.
7. `PdfAXmpPacketRepair`, `PdfASyntaxContent`, `PdfAType1Glyphs`: clean-input early exit, one join, no string round trip per glyph name.

**Core, view models, speech, media, platform**

1. `TextBoxFonts.ReadCovering` reads and parses a whole font file per fallback face on every edit; cache the miss.
2. Speech engines accumulate samples in a growing `List<float>` and copy twice; rent one buffer. Per-inference tensor arrays can be reused.
3. `Signatures/Signing/PdfReader` stream inflate copies three times; use `ZLibDecoder` behind `Compat/`.
4. `FramePump` copies each video frame twice more than needed.
5. `FormRegionDetector.InkOutsideTicks`: vector count with tick windows subtracted; `SignaturePixels` block skip for alpha 0 and 255.
6. `ReadAloudViewModel.MarkWord` allocates two lists per word; `ReadingOffset` and `SentenceAt` are linear scans.
7. `SkiaTypeface.TryGetStream` copies the font on every call; `DocumentSearch` allocates two lists per page without a hit.

`HyperPdf.Skia/NativeMethods.cs` is an accepted native boundary (Skia, for what SkiaSharp does not expose); see the native code rule in `CLAUDE.md`.
