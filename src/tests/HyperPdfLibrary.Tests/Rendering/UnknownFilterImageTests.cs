// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks what is drawn for an image whose filter the library does not know.</summary>
public sealed class UnknownFilterImageTests
{
    /// <summary>The side of the page and of the drawn image, in points and pixels.</summary>
    private const int Side = 32;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The grey of the placeholder box.</summary>
    private const int BoxGrey = 0xE8;

    /// <summary>The most a rendered channel may differ from the placeholder's grey.</summary>
    private const int Tolerance = 4;

    /// <summary>A pixel inside the box and clear of the cross and the frame.</summary>
    private const int ClearColumn = 16;

    /// <summary>The row of the clear pixel.</summary>
    private const int ClearRow = 6;

    /// <summary>The image is drawn as the grey placeholder, not as noise made from its bytes, and the problem is reported.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnknownFilterImageIsDrawnAsAPlaceholder()
    {
        var file = MiniPdf.Build(
            "<< /Type /Catalog /Pages 2 0 R >>",
            "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
            "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 32 32] /Contents 4 0 R /Resources << /XObject << /Im0 5 0 R >> >> >>",
            MiniPdf.Stream(string.Empty, "q 32 0 0 32 0 0 cm /Im0 Do Q"),
            MiniPdf.Stream("/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceGray /BitsPerComponent 8 /Filter /NoSuchDecode", "\0ÿÿ\0"));
        var found = new ConcurrentQueue<PdfDiagnostic>();
        using var document = PdfDocumentReader.OpenWith(file, new PdfOpenOptions { Diagnostics = found.Enqueue, DrawingServices = SkiaTestRegistration.CreateServices() });
        using var renderer = new PdfPageRenderer(document);
        var pixels = new byte[Side * Side * BytesPerPixel];

        var drawn = renderer.Render(new(0, 1, 0, 0, 0, PdfRenderFlags.None), new(pixels, Side, Side, Side * BytesPerPixel));

        const int offset = ((ClearRow * Side) + ClearColumn) * BytesPerPixel;
        await Assert.That(drawn).IsTrue();
        await Assert.That(Math.Abs(pixels[offset] - BoxGrey)).IsLessThanOrEqualTo(Tolerance);
        await Assert.That(Math.Abs(pixels[offset + 1] - BoxGrey)).IsLessThanOrEqualTo(Tolerance);
        await Assert.That(found.Any(static item => item.Code == PdfDiagnosticCode.UnknownFilter)).IsTrue();
    }
}
