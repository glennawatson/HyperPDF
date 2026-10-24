// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Globalization;
using System.Text;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdfLibrary.Tests.Conformance;

/// <summary>Tests for the PDF/A colour, output intent and image rules, one passing and one failing case each.</summary>
public sealed class PdfAColourRulesTests
{
    /// <summary>The PDF/A-2 clause on device colour spaces.</summary>
    private const string DeviceRgbClause = "6.2.4.3";

    /// <summary>The PDF/A-2 clause on images.</summary>
    private const string ImageClause = "6.2.8";

    /// <summary>The PDF/A-2 clause on Separation and DeviceN spaces.</summary>
    private const string SeparationClause = "6.2.4.4";

    /// <summary>The PDF/A-2 clause on ICCBased spaces.</summary>
    private const string IccClause = "6.2.4.2";

    /// <summary>Content that fills a rectangle with DeviceRGB red.</summary>
    private const string RedFill = "1 0 0 rg 0 0 10 10 re f";

    /// <summary>A component count JPEG 2000 data may not have in PDF/A.</summary>
    private const int TwoChannels = 2;

    /// <summary>The bytes of the synthetic ICC profiles: a header and an empty tag table.</summary>
    private const int ProfileLength = 132;

    /// <summary>The offset of the major version in an ICC header.</summary>
    private const int VersionOffset = 8;

    /// <summary>The offset of the device class in an ICC header.</summary>
    private const int ClassOffset = 12;

    /// <summary>The offset of the colour space in an ICC header.</summary>
    private const int SpaceOffset = 16;

    /// <summary>The offset of the file signature in an ICC header.</summary>
    private const int SignatureOffset = 36;

    /// <summary>ICC version 2.</summary>
    private const byte Version2 = 2;

    /// <summary>ICC version 4.</summary>
    private const byte Version4 = 4;

    /// <summary>The components of an RGB profile.</summary>
    private const int RgbComponents = 3;

    /// <summary>The offset of the low byte of the component count in the test JP2 data.</summary>
    private const int ChannelsByte = 37;

    /// <summary>DeviceRGB needs an RGB output intent, and painting with the initial DeviceGray needs any output intent.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DeviceSpacesNeedAMatchingOutputIntent()
    {
        var bare = ConformancePdf.Page();
        bare.Content = RedFill;
        var withIntent = ConformancePdf.Page();
        withIntent.Content = bare.Content;
        ConformancePdf.Intent(withIntent, ConformancePdf.PdfASubtype, Profile("mntr", "RGB ", Version2), RgbComponents);

        await Assert.That(Fails(bare, PdfALevel.PdfA2B, DeviceRgbClause, "2")).IsTrue();
        await Assert.That(Fails(withIntent, PdfALevel.PdfA2B, DeviceRgbClause, "2")).IsFalse();
        await Assert.That(Fails(withIntent, PdfALevel.PdfA2B, "6.2.3", "1")).IsFalse();

        var gray = ConformancePdf.Page();
        gray.Content = "0 0 10 10 re f";
        await Assert.That(Fails(gray, PdfALevel.PdfA2B, DeviceRgbClause, "4")).IsTrue();
    }

    /// <summary>A DefaultRGB space replaces DeviceRGB, so no output intent is needed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultSpaceReplacesDeviceSpace()
    {
        var pdf = ConformancePdf.Page();
        var profile = pdf.AddStream("/N 3", Profile("mntr", "RGB ", Version2));
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/ColorSpace << /DefaultRGB [/ICCBased {profile} 0 R] >>");
        pdf.Content = RedFill;

        await Assert.That(Fails(pdf, PdfALevel.PdfA2B, DeviceRgbClause, "2")).IsFalse();
    }

    /// <summary>PDF/A-1 accepts only ICC version 2 profiles and checks <c>/N</c> against the profile; later parts accept version 4.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task IccProfileVersionAndComponents()
    {
        var pdf = ConformancePdf.Page();
        var profile = pdf.AddStream("/N 4", Profile("prtr", "RGB ", Version4));
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/ColorSpace << /CS0 [/ICCBased {profile} 0 R] >>");
        pdf.Content = "/CS0 cs 0 0 10 10 re f";

        await Assert.That(Fails(pdf, PdfALevel.PdfA1B, "6.2.3.2", "1")).IsTrue();
        await Assert.That(Fails(pdf, PdfALevel.PdfA1B, "6.2.3.2", "2")).IsTrue();
        await Assert.That(Fails(pdf, PdfALevel.PdfA2B, IccClause, "1")).IsFalse();
    }

    /// <summary>Overprinting a CMYK ICCBased colour needs overprint mode 0.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CmykOverprintModeIsChecked()
    {
        var pdf = ConformancePdf.Page();
        var profile = pdf.AddStream("/N 4", Profile("prtr", "CMYK", Version2));
        var state = pdf.AddObject("<< /Type /ExtGState /op true /OPM 1 >>");
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/ColorSpace << /CS0 [/ICCBased {profile} 0 R] >> /ExtGState << /G0 {state} 0 R >>");
        pdf.Content = "/CS0 cs 0 0 0 1 sc 0 0 10 10 re f /G0 gs 0 0 10 10 re f";
        var plain = ConformancePdf.Page();
        var plainProfile = plain.AddStream("/N 4", Profile("prtr", "CMYK", Version2));
        plain.Resources = string.Create(CultureInfo.InvariantCulture, $"/ColorSpace << /CS0 [/ICCBased {plainProfile} 0 R] >>");
        plain.Content = "/CS0 cs 0 0 0 1 sc 0 0 10 10 re f";

        await Assert.That(Fails(pdf, PdfALevel.PdfA2B, IccClause, "2")).IsTrue();
        await Assert.That(Fails(plain, PdfALevel.PdfA2B, IccClause, "2")).IsFalse();
    }

    /// <summary>Images may not ask for interpolation; 16-bit images are allowed from PDF/A-2; masks have one bit.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImageEntriesAreChecked()
    {
        var pdf = ConformancePdf.Page();
        var mask = pdf.AddStream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ImageMask true /BitsPerComponent 2", new byte[1]);
        var image = pdf.AddStream(
            string.Create(CultureInfo.InvariantCulture, $"/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /BitsPerComponent 16 /Interpolate true /Mask {mask} 0 R"),
            new byte[2]);
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/XObject << /Im0 {image} 0 R >>");
        pdf.Content = "q 10 0 0 10 0 0 cm /Im0 Do Q";

        await Assert.That(Fails(pdf, PdfALevel.PdfA2B, ImageClause, "3")).IsTrue();
        await Assert.That(Fails(pdf, PdfALevel.PdfA2B, ImageClause, "4")).IsFalse();
        await Assert.That(Fails(pdf, PdfALevel.PdfA2B, ImageClause, "5")).IsTrue();
        await Assert.That(Fails(pdf, PdfALevel.PdfA1B, "6.2.4", "4")).IsTrue();
    }

    /// <summary>JPEG 2000 data must have 1, 3 or 4 channels.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task JpxChannelsAreChecked()
    {
        await Assert.That(Fails(JpxPage(1), PdfALevel.PdfA2B, "6.2.8.3", "1")).IsFalse();
        await Assert.That(Fails(JpxPage(TwoChannels), PdfALevel.PdfA2B, "6.2.8.3", "1")).IsTrue();
    }

    /// <summary>Halftones must be type 1 or 5, and rendering intents must be one of the four PDF names.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HalftonesAndRenderingIntentsAreChecked()
    {
        var pdf = ConformancePdf.Page();
        var state = pdf.AddObject("<< /Type /ExtGState /HT << /Type /Halftone /HalftoneType 6 /Width 1 /Height 1 >> >>");
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/ExtGState << /G0 {state} 0 R >>");
        pdf.Content = "/G0 gs /Unknown ri";
        var good = ConformancePdf.Page();
        good.Content = "/Perceptual ri";

        await Assert.That(Fails(pdf, PdfALevel.PdfA2B, "6.2.5", "4")).IsTrue();
        await Assert.That(Fails(pdf, PdfALevel.PdfA2B, "6.2.6", "1")).IsTrue();
        await Assert.That(Fails(good, PdfALevel.PdfA2B, "6.2.6", "1")).IsFalse();
    }

    /// <summary>Separations of one colourant must agree, and a DeviceN spot colourant needs a <c>/Colorants</c> entry.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SeparationAndDeviceNAreChecked()
    {
        var pdf = ConformancePdf.Page();
        pdf.Resources = "/ColorSpace << /S0 [/Separation /Spot /DeviceCMYK << /FunctionType 2 /Domain [0 1] /C1 [0 0 0 1] /N 1 >>] "
            + "/S1 [/Separation /Spot /DeviceRGB << /FunctionType 2 /Domain [0 1] /C1 [0 0 0] /N 1 >>] "
            + "/N0 [/DeviceN [/Other] /DeviceGray << /FunctionType 2 /Domain [0 1] /C1 [0] /N 1 >>] >>";
        pdf.Content = "/S0 cs /S1 cs /N0 cs";
        var consistent = ConformancePdf.Page();
        consistent.Resources = "/ColorSpace << /S0 [/Separation /Spot /DeviceCMYK << /FunctionType 2 /Domain [0 1] /C1 [0 0 0 1] /N 1 >>] >>";
        consistent.Content = "/S0 cs /S0 cs";

        await Assert.That(Fails(pdf, PdfALevel.PdfA2B, SeparationClause, "2")).IsTrue();
        await Assert.That(Fails(pdf, PdfALevel.PdfA2B, SeparationClause, "1")).IsTrue();
        await Assert.That(Fails(consistent, PdfALevel.PdfA2B, SeparationClause, "2")).IsFalse();
    }

    /// <summary>Output intents with destination profiles must share one profile object.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OutputIntentsShareOneProfile()
    {
        var pdf = ConformancePdf.Page();
        var first = pdf.AddStream("/N 3", Profile("mntr", "RGB ", Version2));
        var second = pdf.AddStream("/N 3", Profile("mntr", "RGB ", Version2));
        pdf.CatalogEntries = string.Create(
            CultureInfo.InvariantCulture,
            $"/OutputIntents [<< /Type /OutputIntent /S /GTS_PDFA1 /OutputConditionIdentifier (A) /DestOutputProfile {first} 0 R >> "
            + $"<< /Type /OutputIntent /S /GTS_PDFX /OutputConditionIdentifier (B) /DestOutputProfile {second} 0 R >>]");

        await Assert.That(Fails(pdf, PdfALevel.PdfA2B, "6.2.3", "2")).IsTrue();
    }

    /// <summary>PDF/A-4 accepts DeviceRGB inside a page whose transparency group blends in an RGB space.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Part4AcceptsAnRgbBlendingSpace()
    {
        var pdf = ConformancePdf.Page();
        var profile = pdf.AddStream("/N 3", Profile("mntr", "RGB ", Version2));
        pdf.PageEntries = string.Create(CultureInfo.InvariantCulture, $"/Group << /Type /Group /S /Transparency /CS [/ICCBased {profile} 0 R] >>");
        pdf.Content = RedFill;
        var bare = ConformancePdf.Page();
        bare.Content = pdf.Content;

        await Assert.That(Fails(pdf, PdfALevel.PdfA4, DeviceRgbClause, "2")).IsFalse();
        await Assert.That(Fails(bare, PdfALevel.PdfA4, DeviceRgbClause, "2")).IsTrue();
    }

    /// <summary>The checks stop when the token is cancelled.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValidationHonoursCancellation()
    {
        var pdf = ConformancePdf.Page();
        pdf.Content = "0 0 10 10 re f";
        using var document = PdfDocumentReader.Open(pdf.ToBytes(), null);
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        await Assert.That(() => PdfAValidator.Validate(document, PdfALevel.PdfA2B, cancelled.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>Determines whether a file fails a rule at a level.</summary>
    /// <param name="pdf">The file.</param>
    /// <param name="level">The level.</param>
    /// <param name="clause">The clause.</param>
    /// <param name="test">The test number.</param>
    /// <returns><see langword="true"/> when the rule is checked and fails.</returns>
    private static bool Fails(RenderTestPdf pdf, PdfALevel level, string clause, string test) => Fails(pdf.ToBytes(), level, clause, test);

    /// <summary>Determines whether a file fails a rule at a level.</summary>
    /// <param name="bytes">The file.</param>
    /// <param name="level">The level.</param>
    /// <param name="clause">The clause.</param>
    /// <param name="test">The test number.</param>
    /// <returns><see langword="true"/> when the rule is checked and fails.</returns>
    private static bool Fails(byte[] bytes, PdfALevel level, string clause, string test)
    {
        using var document = PdfDocumentReader.Open(bytes, null);
        var report = PdfAValidator.Validate(document, level);
        return Array.Exists(report.Checked, r => r.Clause == clause && r.Test == test && !r.Passed);
    }

    /// <summary>Builds a page that paints a JPEG 2000 image.</summary>
    /// <param name="channels">The image's component count.</param>
    /// <returns>The page.</returns>
    private static RenderTestPdf JpxPage(int channels)
    {
        var pdf = ConformancePdf.Page();
        var image = pdf.AddStream("/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace /DeviceGray /Filter /JPXDecode", Jp2(channels));
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"/XObject << /Im0 {image} 0 R >>");
        pdf.Content = "/Im0 Do";
        return pdf;
    }

    /// <summary>Builds the header of an ICC profile with an empty tag table.</summary>
    /// <param name="deviceClass">The device class signature.</param>
    /// <param name="space">The colour space signature.</param>
    /// <param name="major">The major version.</param>
    /// <returns>The profile bytes.</returns>
    private static byte[] Profile(string deviceClass, string space, byte major)
    {
        var profile = new byte[ProfileLength];
        BinaryPrimitives.WriteUInt32BigEndian(profile, ProfileLength);
        profile[VersionOffset] = major;
        Encoding.ASCII.GetBytes(deviceClass).CopyTo(profile, ClassOffset);
        Encoding.ASCII.GetBytes(space).CopyTo(profile, SpaceOffset);
        "acsp"u8.CopyTo(profile.AsSpan(SignatureOffset));
        return profile;
    }

    /// <summary>Builds the start of a JP2 file: the signature and a header box with an image header of some channels.</summary>
    /// <param name="channels">The number of components.</param>
    /// <returns>The data.</returns>
    private static byte[] Jp2(int channels)
    {
        byte[] data =
        [
            0x00, 0x00, 0x00, 0x0C, 0x6A, 0x50, 0x20, 0x20, 0x0D, 0x0A, 0x87, 0x0A,
            0x00, 0x00, 0x00, 0x2D, 0x6A, 0x70, 0x32, 0x68,
            0x00, 0x00, 0x00, 0x16, 0x69, 0x68, 0x64, 0x72, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x00, 0x01, 0x00, 0x00, 0x07, 0x07, 0x00, 0x00,
            0x00, 0x00, 0x00, 0x0F, 0x63, 0x6F, 0x6C, 0x72, 0x01, 0x00, 0x00, 0x00, 0x00, 0x00, 0x11,
        ];
        data[ChannelsByte] = (byte)channels;
        return data;
    }
}
