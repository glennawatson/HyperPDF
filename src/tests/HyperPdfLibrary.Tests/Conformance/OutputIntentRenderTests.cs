// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Rendering;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Tests that device colours convert through the output intent profile only when the render option is on.</summary>
public sealed class OutputIntentRenderTests
{
    /// <summary>The black component of the test colour.</summary>
    private const string Black = "0.5";

    /// <summary>Cyan, magenta, yellow and black of the test fill.</summary>
    private const string CmykFill = "1 0 0 0.5 k 0 0 100 100 re f";

    /// <summary>A point inside the filled page.</summary>
    private const int Middle = 50;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 2;

    /// <summary>The largest channel difference accepted for images and shadings, which round their samples.</summary>
    private const int ImageTolerance = 4;

    /// <summary>The byte of the black component 0.5.</summary>
    private const byte HalfByte = 128;

    /// <summary>The smallest channel difference that shows two colours apart.</summary>
    private const int Distinct = 60;

    /// <summary>The scale from a 0 to 1 channel to a byte.</summary>
    private const float ByteScale = 255F;

    /// <summary>The flags of an on render.</summary>
    private const PdfRenderFlags On = PdfRenderFlags.OutputIntent;

    /// <summary>The CMYK sample of the test image: cyan full, black half.</summary>
    private static readonly byte[] CmykSample = [byte.MaxValue, 0, 0, HalfByte];

    /// <summary>With the option on, a DeviceCMYK fill is the profile's conversion.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CmykFillUsesTheProfileWhenOn()
    {
        var pdf = IntentPdf(CmykFill);
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage(1, 0, On);

        await Assert.That(image.IsNear(Middle, Middle, ProfileColor(), Tolerance)).IsTrue();
    }

    /// <summary>With the option off, the fill keeps the fixed CMYK conversion.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CmykFillIsUnchangedWhenOff()
    {
        var pdf = IntentPdf(CmykFill);
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(Middle, Middle, DeviceColor(), Tolerance)).IsTrue();
        await Assert.That(Apart(DeviceColor(), ProfileColor())).IsGreaterThan(Distinct);
    }

    /// <summary>One renderer gives the right result as the option is switched on and off.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TogglingTheOptionKeepsBothResultsCorrect()
    {
        var pdf = IntentPdf(CmykFill);
        using var page = new RenderTestPage(pdf.ToBytes());

        var first = page.RenderPage(1, 0, On);
        var second = page.RenderPage();
        var third = page.RenderPage(1, 0, On);

        await Assert.That(first.IsNear(Middle, Middle, ProfileColor(), Tolerance)).IsTrue();
        await Assert.That(second.IsNear(Middle, Middle, DeviceColor(), Tolerance)).IsTrue();
        await Assert.That(third.IsNear(Middle, Middle, ProfileColor(), Tolerance)).IsTrue();
    }

    /// <summary>Concurrent documents keep their own output-intent choice while rendering.</summary>
    /// <param name="testToken">Cancels the concurrent render.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConcurrentDocumentsKeepIntentSelectionsSeparate(CancellationToken testToken)
    {
        var plain = ConformancePdf.Page();
        plain.Content = CmykFill;
        using var intended = new RenderTestPage(IntentPdf(CmykFill).ToBytes());
        using var fixedColors = new RenderTestPage(plain.ToBytes());

        var intendedTask = Task.Run(() => intended.RenderPage(1, 0, On), testToken);
        var fixedTask = Task.Run(() => fixedColors.RenderPage(1, 0, On), testToken);
        var images = await Task.WhenAll(intendedTask, fixedTask);

        await Assert.That(images[0].IsNear(Middle, Middle, ProfileColor(), Tolerance)).IsTrue();
        await Assert.That(images[1].IsNear(Middle, Middle, DeviceColor(), Tolerance)).IsTrue();
    }

    /// <summary>A colour space selected by name converts through the profile as well.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NamedDeviceCmykUsesTheProfile()
    {
        var pdf = IntentPdf($"/DeviceCMYK cs 1 0 0 {Black} sc 0 0 100 100 re f");
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage(1, 0, On);

        await Assert.That(image.IsNear(Middle, Middle, ProfileColor(), Tolerance)).IsTrue();
    }

    /// <summary>A CMYK intent leaves DeviceRGB colours alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RgbIsUnchangedByACmykIntent()
    {
        var pdf = IntentPdf("1 0 0 rg 0 0 100 100 re f");
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage(1, 0, On);

        await Assert.That(image.IsNear(Middle, Middle, Rgb.Red255, Tolerance)).IsTrue();
    }

    /// <summary>A document that claims PDF/A uses its intent without any flag, as ISO 19005 asks of a reader.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfAClaimMakesTheIntentTheDefault()
    {
        var pdf = IntentPdf(CmykFill);
        ConformancePdf.Claim(pdf, 1, "B", null);
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage();

        await Assert.That(image.IsNear(Middle, Middle, ProfileColor(), Tolerance)).IsTrue();
    }

    /// <summary>A DeviceCMYK image converts through the profile with the option on, and keeps the table with it off.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CmykImageUsesTheProfile()
    {
        var pdf = IntentPdf("q 100 0 0 100 0 0 cm /Im Do Q");
        var image = pdf.AddStream(
            "/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceCMYK /BitsPerComponent 8",
            CmykSample);
        pdf.Resources = $"/XObject << /Im {image} 0 R >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var on = page.RenderPage(1, 0, On);
        var off = page.RenderPage();

        await Assert.That(on.IsNear(Middle, Middle, ProfileColor(), ImageTolerance)).IsTrue();
        await Assert.That(off.IsNear(Middle, Middle, DeviceColor(), ImageTolerance)).IsTrue();
    }

    /// <summary>A DeviceCMYK shading converts through the profile with the option on.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CmykShadingUsesTheProfile()
    {
        var pdf = IntentPdf("/Sh sh");
        pdf.Resources = "/Shading << /Sh << /ShadingType 2 /ColorSpace /DeviceCMYK /Coords [0 0 100 0] "
            + "/Function << /FunctionType 2 /Domain [0 1] /C0 [1 0 0 0.5] /C1 [1 0 0 0.5] /N 1 >> >> >>";
        using var page = new RenderTestPage(pdf.ToBytes());

        var on = page.RenderPage(1, 0, On);
        var off = page.RenderPage();

        await Assert.That(on.IsNear(Middle, Middle, ProfileColor(), ImageTolerance)).IsTrue();
        await Assert.That(off.IsNear(Middle, Middle, DeviceColor(), ImageTolerance)).IsTrue();
    }

    /// <summary>A document with no output intent renders the same with the option on.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoIntentMeansNoChange()
    {
        var pdf = ConformancePdf.Page();
        pdf.Content = CmykFill;
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage(1, 0, On);

        await Assert.That(image.IsNear(Middle, Middle, DeviceColor(), Tolerance)).IsTrue();
    }

    /// <summary>An intent whose profile cannot be read leaves colours alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnreadableProfileMeansNoChange()
    {
        var pdf = ConformancePdf.Page();
        pdf.Content = CmykFill;
        ConformancePdf.Intent(pdf, ConformancePdf.PdfASubtype, "not a profile"u8.ToArray(), ConformancePdf.CmykComponents);
        using var page = new RenderTestPage(pdf.ToBytes());

        var image = page.RenderPage(1, 0, On);

        await Assert.That(image.IsNear(Middle, Middle, DeviceColor(), Tolerance)).IsTrue();
    }

    /// <summary>Builds a page with a CMYK PDF/A output intent.</summary>
    /// <param name="content">The page content.</param>
    /// <returns>The builder.</returns>
    private static RenderTestPdf IntentPdf(string content)
    {
        var pdf = ConformancePdf.Page();
        pdf.Content = content;
        ConformancePdf.Intent(pdf, ConformancePdf.PdfASubtype, ConformancePdf.CmykProfile(), ConformancePdf.CmykComponents);
        return pdf;
    }

    /// <summary>Converts the test colour with the profile directly.</summary>
    /// <returns>The colour.</returns>
    private static Rgb ProfileColor()
    {
        var transform = IccProfile.Create(ConformancePdf.CmykProfile())!;
        Span<float> rgb = stackalloc float[PdfColorSpace.RgbComponents];
        transform.ToRgb([1, 0, 0, float.Parse(Black, System.Globalization.CultureInfo.InvariantCulture)], rgb);
        return ToBytes(rgb);
    }

    /// <summary>Converts the test colour with the fixed DeviceCMYK table.</summary>
    /// <returns>The colour.</returns>
    private static Rgb DeviceColor()
    {
        Span<float> rgb = stackalloc float[PdfColorSpace.RgbComponents];
        PdfColorSpace.DeviceCmyk.ToRgb([1, 0, 0, float.Parse(Black, System.Globalization.CultureInfo.InvariantCulture)], rgb);
        return ToBytes(rgb);
    }

    /// <summary>Rounds 0 to 1 channels to bytes.</summary>
    /// <param name="rgb">The channels.</param>
    /// <returns>The colour.</returns>
    private static Rgb ToBytes(ReadOnlySpan<float> rgb) =>
        new((int)MathF.Round(rgb[0] * ByteScale), (int)MathF.Round(rgb[1] * ByteScale), (int)MathF.Round(rgb[2] * ByteScale));

    /// <summary>Gets the largest channel difference between two colours.</summary>
    /// <param name="a">The first colour.</param>
    /// <param name="b">The second colour.</param>
    /// <returns>The difference.</returns>
    private static int Apart(Rgb a, Rgb b) => Math.Max(Math.Abs(a.Red - b.Red), Math.Max(Math.Abs(a.Green - b.Green), Math.Abs(a.Blue - b.Blue)));
}
