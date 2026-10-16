# HyperPDF End-to-End Document Creation and Rendering Example

This folder contains a complete, self-contained example demonstrating how to:

1. Create a tagged PDF document with [`PdfDocumentCreator`](../HyperPdfLibrary/Creation/PdfDocumentCreator.cs) and embed bundled fonts via [`PdfBundledFonts.Open`](../HyperPdfLibrary/Creation/PdfBundledFonts.cs).
2. Generate or decode an image with **SkiaSharp** (`SKBitmap` in `SKColorType.Bgra8888`) and pass its raw pixel bytes into [`PdfRasterImage`](../HyperPdfLibrary/Creation/PdfRasterImage.cs) (`PdfRasterFormat.Bgra32`) via `creator.AddRaster(...)`.
3. Lay out multi-block content with [`PdfTextFlow`](../HyperPdfLibrary/Creation/PdfTextFlow.cs), including:
   - Headings ([`PdfFlowHeading`](../HyperPdfLibrary/Creation/PdfFlowHeading.cs)) with automatic `KeepWithNext` pagination,
   - Horizontal rules ([`PdfFlowRule`](../HyperPdfLibrary/Creation/PdfFlowRule.cs)),
   - Paragraphs ([`PdfFlowParagraph`](../HyperPdfLibrary/Creation/PdfFlowParagraph.cs)),
   - Images ([`PdfFlowImage`](../HyperPdfLibrary/Creation/PdfFlowImage.cs)) with configurable [`PdfFlowImageFit`](../HyperPdfLibrary/Creation/PdfFlowImageFit.cs) and alignment, and
   - Tables ([`PdfFlowTable`](../HyperPdfLibrary/Creation/PdfFlowTable.cs)) with `FillAvailableWidth` and relative/fixed [`PdfTableColumnWidth`](../HyperPdfLibrary/Creation/PdfTableColumnWidth.cs) sizing.
4. Finish and save the PDF (`creator.Finish()` + `creator.ToArray()`), reopen it with [`PdfDocumentReader`](../HyperPdfLibrary/Document/PdfDocumentReader.cs), extract text with [`PdfDocumentText`](../HyperPdfLibrary/Document/PdfDocumentText.cs), and render pages to a PNG image using [`PdfPageRenderer`](../HyperPdfLibrary/Rendering/PdfPageRenderer.cs) and `HyperPdfLibrary.Render.Skia`.

## Running the Example

Run the example from the repository root:

```bash
dotnet run --project src/examples/HyperPdf.Examples.csproj -f net11.0
```

Pass an optional output directory to write `example.pdf` and `page-1.png` to disk:

```bash
dotnet run --project src/examples/HyperPdf.Examples.csproj -f net11.0 -- ./artifacts/example-output
```

> **Note:** `HyperPdf.Examples.csproj` sets `<IsPackable>false</IsPackable>` and `<IsPublishable>false</IsPublishable>` so it is never packaged into NuGet artefacts or included in publish outputs.

---

## Step-by-Step Walkthrough

### 1. Extracting Raw Pixel Bytes from SkiaSharp (`PdfRasterImage`)

`PdfDocumentCreator.AddRaster` accepts raw pixel bytes wrapped in a `PdfRasterImage`. When using `SkiaSharp`, create or decode an `SKBitmap` with `SKColorType.Bgra8888` and pass `bitmap.Bytes` with `PdfRasterFormat.Bgra32`:

```csharp
using HyperPdfLibrary.Creation;
using SkiaSharp;

var info = new SKImageInfo(240, 80, SKColorType.Bgra8888, SKAlphaType.Premul);
using var bitmap = new SKBitmap(info);
using (var canvas = new SKCanvas(bitmap))
{
    canvas.Clear(new SKColor(30, 64, 124));
}

// Obtain the raw BGRA32 pixel bytes directly from the SkiaSharp bitmap:
byte[] rawBytes = bitmap.Bytes;
var raster = new PdfRasterImage(PdfRasterFormat.Bgra32, bitmap.Width, bitmap.Height, rawBytes);
PdfCreatedImage bannerImage = creator.AddRaster(raster, PdfImageOptions.Default, CancellationToken.None);
```

If you are loading an existing PNG/WebP/BMP file with SkiaSharp, decode it into `SKColorType.Bgra8888` first:

```csharp
using var codec = SKCodec.Create("logo.png");
var targetInfo = new SKImageInfo(codec.Info.Width, codec.Info.Height, SKColorType.Bgra8888, SKAlphaType.Premul);
using var decoded = SKBitmap.Decode(codec, targetInfo);
var image = creator.AddRaster(
    new PdfRasterImage(PdfRasterFormat.Bgra32, decoded.Width, decoded.Height, decoded.Bytes),
    PdfImageOptions.Default,
    CancellationToken.None);
```

### 2. Creating a Flow Document (`PdfDocumentCreator` + `PdfTextFlow`)

Embed the bundled fonts and pass flow blocks (`PdfFlowHeading`, `PdfFlowRule`, `PdfFlowParagraph`, `PdfFlowImage`, `PdfFlowTable`) to `PdfTextFlow.Add`:

```csharp
var creator = new PdfDocumentCreator(new PdfDocumentCreatorOptions
{
    Title = "HyperPDF End-to-End Example",
    Author = "HyperPDF",
    Language = "en-AU",
    Tagged = true,
    UaClaim = PdfUaConformance.Part2,
});

var bodyFont = creator.EmbedFont(PdfBundledFonts.Open(PdfBundledFontFamily.Sans, bold: false, italic: false).Source!);
var headingFont = creator.EmbedFont(PdfBundledFonts.Open(PdfBundledFontFamily.Sans, bold: true, italic: false).Source!);

var flowOptions = new PdfFlowOptions(bodyFont)
{
    HeadingFont = headingFont,
};

PdfFlowBlock[] blocks =
[
    new PdfFlowHeading(1, "HyperPDF End-to-End Report"),
    new PdfFlowRule
    {
        Thickness = 1.5F,
        Color = new PdfRgbColor(0.2F, 0.35F, 0.6F),
    },
    new PdfFlowParagraph("This document was created with PdfDocumentCreator and PdfTextFlow."),
    new PdfFlowImage(bannerImage, "Decorative blue banner", 240F)
    {
        Alignment = PdfTableAlignment.Center,
        Fit = PdfFlowImageFit.ScaleToPage,
    },
    new PdfFlowHeading(1, "Component Status"),
    new PdfFlowTable(
        [
            PdfTableRow.FromText("Component", "Description", "Status"),
            PdfTableRow.FromText("PdfDocumentCreator", "Creates PDF 1.7 / 2.0 documents with bundled fonts.", "Ready"),
            PdfTableRow.FromText("PdfTextFlow", "Flows headings, paragraphs, rules, images, and tables.", "Ready"),
        ])
    {
        FillAvailableWidth = true,
        ColumnSizing =
        [
            PdfTableColumnWidth.Proportional(1F),
            PdfTableColumnWidth.Proportional(3F),
            PdfTableColumnWidth.Fixed(90F),
        ],
    },
];

PdfTextFlow.Add(creator, blocks, flowOptions, CancellationToken.None);
creator.Finish();
byte[] pdfBytes = creator.ToArray();
```

### 3. Reading Text and Rendering to PNG with SkiaSharp (`PdfPageRenderer`)

Open the generated PDF with `PdfDrawingServices` configured to use the Skia backend (`new PdfRenderBackendRegistration().UseSkia()`), render page 0 into a premultiplied BGRA8888 buffer, and encode to PNG with SkiaSharp:

```csharp
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Render.Skia;
using HyperPdfLibrary.Rendering;

var drawing = new PdfDrawingServices(new PdfRenderBackendRegistration().UseSkia());
using var document = await PdfDocumentReader.OpenWithAsync(
    pdfBytes,
    new PdfOpenOptions { DrawingServices = drawing },
    CancellationToken.None);

var textPage = await PdfDocumentText.GetTextPageAsync(document, 0, CancellationToken.None);

const float scale = 2F;
var page = PdfDocumentPages.GetPage(document, 0);
var width = (int)MathF.Ceiling(page.Width * scale);
var height = (int)MathF.Ceiling(page.Height * scale);
var stride = width * 4;
var pixels = new byte[height * stride];

using var renderer = new PdfPageRenderer(document);
await renderer.RenderAsync(
    new PdfTileRequest(0, scale, 0, 0, 0, PdfRenderFlags.Annotations),
    pixels,
    width,
    height,
    stride,
    CancellationToken.None);

var info = new SKImageInfo(width, height, SKColorType.Bgra8888, SKAlphaType.Premul);
using var renderedBitmap = new SKBitmap(info);
pixels.AsSpan().CopyTo(renderedBitmap.GetPixelSpan());
using var skImage = SKImage.FromBitmap(renderedBitmap);
using var pngData = skImage.Encode(SKEncodedImageFormat.Png, 100);
byte[] pngBytes = pngData.ToArray();
```
