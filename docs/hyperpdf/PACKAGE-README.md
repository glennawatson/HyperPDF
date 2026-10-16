# HyperPdfLibrary

A managed PDF engine for .NET 10 and .NET 11. It reads, searches and annotates PDF files, and renders through a drawing backend you choose. It works on Linux, Windows and macOS and is trim and Native AOT safe. Reading and editing with the core package needs no native drawing library.

HyperPdfLibrary is the engine behind [Hyper PDF Viewer](https://github.com/glennawatson/HyperPDF).

## Open a document and read its text

```csharp
using HyperPdfLibrary.Document;

using var document = await PdfDocumentReader.OpenAsync("input.pdf", password: null, CancellationToken.None);
Console.WriteLine($"{document.PageCount} pages");
Console.WriteLine((await PdfDocumentText.GetTextPageAsync(document, 0, CancellationToken.None)).Text);
```

## Render a page

Install `HyperPdfLibrary.Render.Skia` to use the Skia backend. Pass drawing services when opening a document. You can supply your own backend through the contracts in `HyperPdfLibrary.Drawing`. Documents keep the services selected at open, so separate engines can use different providers.

For image formats the library cannot decode, set `PdfRenderBackendRegistration.UnsupportedImageDecoder`. The callback receives the image stream and the render operation's cancellation token.

The renderer writes premultiplied BGRA pixels into a buffer you own. `Scale` is device pixels per PDF point.

```csharp
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Render.Skia;
using HyperPdfLibrary.Rendering;

var drawing = new PdfDrawingServices(new PdfRenderBackendRegistration().UseSkia());
using var document = await PdfDocumentReader.OpenWithAsync("input.pdf", new PdfOpenOptions { DrawingServices = drawing }, CancellationToken.None);

const float scale = 2f;
var page = PdfDocumentPages.GetPage(document, 0);
var width = (int)MathF.Ceiling(page.Width * scale);
var height = (int)MathF.Ceiling(page.Height * scale);
var pixels = new byte[width * height * 4];

using var renderer = new PdfPageRenderer(document);
await renderer.RenderAsync(new PdfTileRequest(0, scale, 0, 0, 0, PdfRenderFlags.Annotations), pixels, width, height, width * 4, CancellationToken.None);
```

A document can be used from many render threads at once.

## Fonts, offline use and external data

The fonts that stand in for the 14 standard PDF fonts are embedded in the library assembly: Arimo (Helvetica, Arial), Tinos (Times), Cousine (Courier), Noto Sans Math (Symbol) and Noto Sans Symbols 2 (ZapfDingbats). Each is cut down to Latin, Greek, Cyrillic, Hebrew, Vietnamese, punctuation, currency, arrows, mathematical operators, box drawing and geometric shapes (the two symbol faces to the characters of Symbol and ZapfDingbats), and adds about 2 MB to the package. They are never downloaded. Creating documents, saving, PDF/A conversion and substituting a non-embedded standard font make no network request.

Create a document with an embedded font, a SkiaSharp raster image, and flowed content:

```csharp
using HyperPdfLibrary.Creation;
using SkiaSharp;

var creator = new PdfDocumentCreator(PdfDocumentCreatorOptions.Default);
var font = creator.EmbedFont(PdfBundledFonts.Open(PdfBundledFontFamily.Sans, bold: false, italic: false).Source!);

using var bitmap = new SKBitmap(new SKImageInfo(240, 80, SKColorType.Bgra8888, SKAlphaType.Premul));
var raster = new PdfRasterImage(PdfRasterFormat.Bgra32, bitmap.Width, bitmap.Height, bitmap.Bytes);
var image = creator.AddRaster(raster, PdfImageOptions.Default, CancellationToken.None);

PdfTextFlow.Add(
    creator,
    [
        new PdfFlowHeading(1, "Report"),
        new PdfFlowRule(),
        new PdfFlowParagraph("Body text flowed across pages."),
        new PdfFlowImage(image, "Banner", 240f),
    ],
    new PdfFlowOptions(font),
    CancellationToken.None);

creator.Finish();
File.WriteAllBytes("output.pdf", creator.ToArray());
```

See [`src/examples`](https://github.com/glennawatson/HyperPDF/tree/main/src/examples) for a complete end-to-end document creation and SkiaSharp rendering example.

Text outside the cut-down ranges draws the font's missing glyph; embed your own font for other scripts.

Creating a document never contacts the network. `PdfDocumentCreator`, `PdfBundledFonts` and font embedding have no network code, and the embedded fonts are inside the assembly. The font data setting below applies only when you open, read or render a page.

Only the predefined CJK CMaps and the CID to Unicode tables are external. A page needs them when a composite font names a predefined CMap (such as `90ms-RKSJ-H` or `UniJIS-UCS2-H`) instead of embedding it, or when a non-embedded CJK font needs text. The engine generates them from compact binary resources of the PDF.js CMap pack, only when a requested page needs them, and caches only the requested assets under `HyperPdfLibrary/FontData/v3` in the user's local application data folder. CJK text in a non-embedded font is drawn with a system font from your font provider; the library embeds no CJK font.

Set `PdfDrawingServices.FontDataMode` to choose what happens when that data is not cached:

- `PdfFontDataMode.OnDemand` (the default) downloads the missing data the first time a page needs it. A failed download throws from the asynchronous methods.
- `PdfFontDataMode.Offline` never contacts the network. Cached data is used. Missing data is skipped, and `GetMissingFontData()` lists it (for example `CMaps/90ms-RKSJ-H.bin`) in ordinal order. A composite font whose predefined CMap is missing reads its two-byte codes as CIDs and has no extracted text of its own; a non-embedded CJK font without its Unicode table has no text. The same inputs always give the same result. To supply the data, copy the listed files into the font data folder from a machine that is online, or run once in `OnDemand` mode.

Use `PdfDocumentText.GetTextPageAsync`, `PdfDocumentPages.PrefetchPageAsync`, `PdfFontLoader.LoadAsync` or the renderer's `RenderAsync` for first use. These await missing font data before the synchronous core runs. The synchronous APIs use cached resources. Documents using the same `PdfDrawingServices` reuse parsed font data in memory; later runs reuse the generated files without downloading them again. The first request for an uncached resource needs internet access. Set `HYPERPDF_FONT_DATA` before first use to choose a different cache root.

Identity CMaps and fonts embedded in the PDF need no substitute font downloads. PDFs with complete embedded CMaps and ToUnicode mappings need no external CMap data.

## Licence

HyperPdfLibrary is MIT licensed. CJK mappings use the PDF.js binary CMap resource pack under Adobe's BSD licence. The embedded substitute faces are Arimo (Apache-2.0), Tinos and Cousine (SIL OFL 1.1), and Noto Sans Math and Noto Sans Symbols 2 (SIL OFL 1.1), cut down to the characters the standard fonts need. Their notices ship in this package (`Faces-LICENSE.txt`, `THIRD-PARTY-NOTICES.md`).

Report problems at [GitHub issues](https://github.com/glennawatson/HyperPDF/issues).
