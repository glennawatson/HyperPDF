// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Image <c>/Alternates</c> and form <c>/Ref</c> do not change what is drawn: the main image and the proxy form are (ISO 32000-2 §8.9.5.4, §8.10.4).</summary>
public sealed class ImageAlternateRenderTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 6;

    /// <summary>A coordinate inside the first image column or row.</summary>
    private const int Near = 25;

    /// <summary>A coordinate inside the second image column or row.</summary>
    private const int Far = 75;

    /// <summary>The entries of the 2x2 RGB main image.</summary>
    private const string MainEntries = "/Type /XObject /Subtype /Image /Width 2 /Height 2 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /ASCIIHexDecode";

    /// <summary>The main image: red and green on the top row, blue and yellow below.</summary>
    private const string MainData = "FF0000 00FF00 0000FF FFFF00>";

    /// <summary>The entries of the alternate image, one magenta pixel.</summary>
    private const string AlternateEntries = "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceRGB /BitsPerComponent 8 /Filter /ASCIIHexDecode";

    /// <summary>The alternate image's data.</summary>
    private const string AlternateData = "FF00FF>";

    /// <summary>Draws the form named Fm over the whole page.</summary>
    private const string DrawForm = "/Fm Do";

    /// <summary>Fills the form's box with blue.</summary>
    private const string BlueFill = "0 0 1 rg 0 0 100 100 re f";

    /// <summary>The form entries shared by the reference samples.</summary>
    private const string FormEntries = "/Type /XObject /Subtype /Form /BBox [0 0 100 100]";

    /// <summary>The main image is drawn even when an alternate is marked as the printing default.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MainImageIsDrawnWhenAlternatesExist()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "q 100 0 0 100 0 0 cm /Im Do Q" };
        var alternate = pdf.AddStream(AlternateEntries, AlternateData);
        var image = pdf.AddStream($"{MainEntries} /Alternates [<< /Image {alternate} 0 R /DefaultForPrinting true >>]", MainData);
        pdf.Resources = $"/XObject << /Im {image} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var result = page.RenderPage();

        await Assert.That(result.IsNear(Near, Near, Rgb.Red255, Tolerance)).IsTrue();
        await Assert.That(result.IsNear(Far, Near, Rgb.Green255, Tolerance)).IsTrue();
        await Assert.That(result.IsNear(Near, Far, Rgb.Blue255, Tolerance)).IsTrue();
        await Assert.That(result.IsNear(Far, Far, Rgb.Yellow, Tolerance)).IsTrue();
    }

    /// <summary>An image whose alternates list itself and each other is drawn once and does not loop.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CyclicAlternatesStillDrawTheMainImage()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "q 100 0 0 100 0 0 cm /Im Do Q" };
        var main = pdf.NextNumber;
        var alternate = main + 1;
        _ = pdf.AddStream($"{MainEntries} /Alternates [<< /Image {main} 0 R >> << /Image {alternate} 0 R >>]", MainData);
        _ = pdf.AddStream($"{AlternateEntries} /Alternates [<< /Image {main} 0 R >> << /Image {alternate} 0 R >>]", AlternateData);
        pdf.Resources = $"/XObject << /Im {main} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var result = page.RenderPage();

        await Assert.That(result.IsNear(Near, Near, Rgb.Red255, Tolerance)).IsTrue();
        await Assert.That(result.IsNear(Far, Far, Rgb.Yellow, Tolerance)).IsTrue();
    }

    /// <summary>A form XObject with a <c>/Ref</c> draws its own content, the proxy.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReferenceFormDrawsItsProxy()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = DrawForm };
        var form = pdf.AddStream($"{FormEntries} /Ref << /F (missing-target.pdf) /Page 0 >>", BlueFill);
        pdf.Resources = $"/XObject << /Fm {form} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var result = page.RenderPage();

        await Assert.That(result.IsNear(Near, Near, Rgb.Blue255, Tolerance)).IsTrue();
        await Assert.That(result.IsNear(Far, Far, Rgb.Blue255, Tolerance)).IsTrue();
    }

    /// <summary>A <c>/Ref</c> naming a PDF that exists is not opened: the target's red page does not replace the proxy.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReferenceToAnExistingFileIsNotFollowed()
    {
        var targetPdf = new RenderTestPdf(Size, Size) { Content = "1 0 0 rg 0 0 100 100 re f" }.ToBytes();
        var path = Path.Combine(Path.GetTempPath(), string.Create(CultureInfo.InvariantCulture, $"hyperpdf-ref-target-{Guid.NewGuid():N}.pdf"));
        await File.WriteAllBytesAsync(path, targetPdf);
        try
        {
            var pdf = new RenderTestPdf(Size, Size) { Content = DrawForm };
            var form = pdf.AddStream($"{FormEntries} /Ref << /F ({path}) /Page 0 >>", BlueFill);
            pdf.Resources = $"/XObject << /Fm {form} 0 R >>";
            using var page = new RenderTestPage(pdf.ToBytes());

            var result = page.RenderPage();

            await Assert.That(result.IsNear(Near, Near, Rgb.Blue255, Tolerance)).IsTrue();
            await Assert.That(result.IsNear(Far, Far, Rgb.Blue255, Tolerance)).IsTrue();
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>A proxy form that draws itself stops at the drawing depth limit and keeps what it drew.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SelfDrawingReferenceFormStops()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = DrawForm };
        var form = pdf.NextNumber;
        _ = pdf.AddStream($"{FormEntries} /Ref << /F (missing-target.pdf) /Page 0 >> /Resources << /XObject << /Fm {form} 0 R >> >>", $"{BlueFill} {DrawForm}");
        pdf.Resources = $"/XObject << /Fm {form} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var result = page.RenderPage();

        await Assert.That(result.IsNear(Near, Near, Rgb.Blue255, Tolerance)).IsTrue();
    }
}
