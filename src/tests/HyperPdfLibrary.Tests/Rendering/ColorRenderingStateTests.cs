// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Numerics;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Graphics.Shadings;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Graphics;
using static HyperPdfLibrary.Tests.Graphics.IccTestProfileBuilder;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>
/// Checks that the rendering intent (<c>ri</c>, /RI, image /Intent) and PDF 2.0 /UseBlackPtComp reach the graphics state,
/// survive <c>q</c> and <c>Q</c>, and choose the ICC conversion of fills, strokes, text, shadings and images.
/// </summary>
public sealed class ColorRenderingStateTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The middle of the page.</summary>
    private const int Middle = 50;

    /// <summary>The components of CMYK.</summary>
    private const int CmykComponents = 4;

    /// <summary>The largest channel difference accepted between a drawn image and its expected colour.</summary>
    private const int ImageTolerance = 2;

    /// <summary>The stored L* of the perceptual table in the two-table profile, as a fraction of 100.</summary>
    private const double DarkLightness = 0.3;

    /// <summary>The stored L* of the colorimetric table in the two-table profile, as a fraction of 100.</summary>
    private const double LightLightness = 0.8;

    /// <summary>The stored value of a zero a* or b* in an 'mft1' table.</summary>
    private const double NeutralChroma = 128.0 / 255.0;

    /// <summary>The grid points of a table with only the corners.</summary>
    private const int CornerGrid = 2;

    /// <summary>The position of red in a BGRA pixel.</summary>
    private const int RedByte = 2;

    /// <summary>The <c>sh</c> operators in the shading test.</summary>
    private const int ShadingPaints = 2;

    /// <summary>The graphics states the tests name.</summary>
    private const string GraphicsStates =
        "/ExtGState << /On << /UseBlackPtComp /ON /RI /Perceptual >> /Off << /UseBlackPtComp /OFF /RI /Saturation >> "
        + "/Def << /UseBlackPtComp /Default >> /Lower << /UseBlackPtComp /on /RI 3 >> /Text << /UseBlackPtComp (ON) >> "
        + "/Flag << /UseBlackPtComp true /RI [/Perceptual] >> /Odd << /RI /Vivid >> /Abs << /RI /AbsoluteColorimetric /UseBlackPtComp /ON >> >>";

    /// <summary>An axial shading that paints the rich black of /CS0 everywhere.</summary>
    private const string Shading =
        "/Shading << /Sh0 << /ShadingType 2 /ColorSpace /CS0 /Coords [0 0 1 0] "
        + "/Function << /FunctionType 2 /Domain [0 1] /C0 [1 1 1 1] /C1 [1 1 1 1] /N 1 >> >> >>";

    /// <summary>A unit square fill that records the state.</summary>
    private const string Paint = " 0 0 1 1 re f ";

    /// <summary>The rich black of the CMYK test profile.</summary>
    private static readonly float[] RichBlack = [1, 1, 1, 1];

    /// <summary><c>gs</c> sets the intent and black point choice; <c>q</c> saves them and <c>Q</c> restores them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GraphicsStateKeysAreSetAndRestored()
    {
        var device = Run($"{Paint}q /On gs{Paint}q /Off gs{Paint}Q{Paint}Q{Paint}/On gs /Def gs{Paint}");

        await AssertState(device.Paints[0], IccIntent.RelativeColorimetric, BlackPointCompensationMode.Default);
        await AssertState(device.Paints[1], IccIntent.Perceptual, BlackPointCompensationMode.On);
        await AssertState(device.Paints[2], IccIntent.Saturation, BlackPointCompensationMode.Off);
        await AssertState(device.Paints[3], IccIntent.Perceptual, BlackPointCompensationMode.On);
        await AssertState(device.Paints[4], IccIntent.RelativeColorimetric, BlackPointCompensationMode.Default);
        await AssertState(device.Paints[5], IccIntent.Perceptual, BlackPointCompensationMode.Default);
    }

    /// <summary>Misspelled, string, boolean and number values leave the state unchanged; an unknown intent name reads as relative colorimetric.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MalformedValuesAreIgnored()
    {
        var device = Run($"/Off gs /Lower gs{Paint}/Text gs /Flag gs{Paint}/Odd gs{Paint}");

        await AssertState(device.Paints[0], IccIntent.Saturation, BlackPointCompensationMode.Off);
        await AssertState(device.Paints[1], IccIntent.Saturation, BlackPointCompensationMode.Off);
        await AssertState(device.Paints[^1], IccIntent.RelativeColorimetric, BlackPointCompensationMode.Off);
    }

    /// <summary><c>ri</c> sets the intent, ignores an operand that is not a name, reads an unknown name as relative colorimetric, and <c>Q</c> restores it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RenderingIntentOperatorIsReadAndRestored()
    {
        var device = Run($"/Perceptual ri q /Saturation ri{Paint}5 ri{Paint}Q{Paint}/Vivid ri{Paint}");

        await AssertState(device.Paints[0], IccIntent.Saturation, BlackPointCompensationMode.Default);
        await AssertState(device.Paints[1], IccIntent.Saturation, BlackPointCompensationMode.Default);
        await AssertState(device.Paints[2], IccIntent.Perceptual, BlackPointCompensationMode.Default);
        await AssertState(device.Paints[^1], IccIntent.RelativeColorimetric, BlackPointCompensationMode.Default);
    }

    /// <summary>
    /// A PDF 1.7 file and a PDF 2.0 file without the key start with relative colorimetric and /Default, which compensates
    /// for black points, so an ICC fill converts with relative colorimetric and compensation.
    /// </summary>
    /// <param name="version">The header version.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("1.7")]
    [Arguments("2.0")]
    public async Task FilesWithoutTheKeyKeepTheDefaults(string version)
    {
        var device = Run($"/CS0 cs 1 1 1 1 scn{Paint}", BlackPointCompensationTests.CmykProfile(), version);
        var space = BlackPointCompensationTests.IccSpace(BlackPointCompensationTests.CmykProfile(), CmykComponents);

        await AssertState(device.Paints[0], IccIntent.RelativeColorimetric, BlackPointCompensationMode.Default);
        await Assert.That(device.Paints[0].Rendering).IsEqualTo(new(IccIntent.RelativeColorimetric, true));
        await Assert.That(device.Paints[0].Fill).IsEqualTo(Expected(space, IccRendering.Default));
    }

    /// <summary>ON and OFF give different fills and strokes, including for a colour set before the graphics state changed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OnAndOffGiveDistinctFillsAndStrokes()
    {
        var device = Run($"/CS0 cs 1 1 1 1 scn /CS0 CS 1 1 1 1 SCN /On gs{Paint}/Off gs{Paint}0 0 1 1 re S", BlackPointCompensationTests.CmykProfile(), "2.0");
        var space = BlackPointCompensationTests.IccSpace(BlackPointCompensationTests.CmykProfile(), CmykComponents);
        var on = Expected(space, new(IccIntent.Perceptual, true));
        var off = Expected(space, new(IccIntent.Saturation, false));

        await Assert.That(on).IsNotEqualTo(off);
        await Assert.That(device.Paints[0].Fill).IsEqualTo(on);
        await Assert.That(device.Paints[1].Fill).IsEqualTo(off);
        await Assert.That(device.Paints[^1].Stroke).IsEqualTo(off);
    }

    /// <summary>Absolute colorimetric with ON converts as OFF does (ISO 32000-2 8.6.5.9).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AbsoluteColorimetricTurnsCompensationOff()
    {
        var device = Run($"/CS0 cs 1 1 1 1 scn /Abs gs{Paint}", BlackPointCompensationTests.CmykProfile(), "2.0");
        var space = BlackPointCompensationTests.IccSpace(BlackPointCompensationTests.CmykProfile(), CmykComponents);

        await Assert.That(device.Paints[0].Rendering.Compensate).IsFalse();
        await Assert.That(device.Paints[0].Fill).IsEqualTo(Expected(space, new(IccIntent.RelativeColorimetric, false)));
    }

    /// <summary>The intent picks the profile's table: perceptual uses AToB0 and the colorimetric intents AToB1.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RenderingIntentSelectsTheConversion()
    {
        var profile = TwoTableProfile();
        var device = Run($"/CS0 cs 1 1 1 1 scn{Paint}/Perceptual ri{Paint}/AbsoluteColorimetric ri{Paint}", profile, "1.7");
        var space = BlackPointCompensationTests.IccSpace(profile, CmykComponents);

        await Assert.That(device.Paints[0].Fill).IsEqualTo(Expected(space, IccRendering.Default));
        await Assert.That(device.Paints[1].Fill).IsEqualTo(Expected(space, new(IccIntent.Perceptual, true)));
        await Assert.That(device.Paints[2].Fill).IsEqualTo(Expected(space, new(IccIntent.AbsoluteColorimetric, false)));
        await Assert.That(device.Paints[1].Fill).IsNotEqualTo(device.Paints[0].Fill);
    }

    /// <summary>Glyphs paint with the fill colour of the rendering in force.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextFollowsTheRendering()
    {
        var device = Run("/CS0 cs 1 1 1 1 scn /Off gs BT /F1 10 Tf (A) Tj ET", BlackPointCompensationTests.CmykProfile(), "2.0");
        var space = BlackPointCompensationTests.IccSpace(BlackPointCompensationTests.CmykProfile(), CmykComponents);

        await Assert.That(device.Glyphs).IsGreaterThan(0);
        await Assert.That(device.Paints[^1].Fill).IsEqualTo(Expected(space, new(IccIntent.Saturation, false)));
    }

    /// <summary><c>sh</c> converts the shading's ICC colours with the rendering in force.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ShadingFollowsTheRendering()
    {
        var device = Run("/Sh0 sh /Off gs /Sh0 sh", BlackPointCompensationTests.CmykProfile(), "2.0");
        var space = BlackPointCompensationTests.IccSpace(BlackPointCompensationTests.CmykProfile(), CmykComponents);

        await Assert.That(device.Shadings.Count).IsEqualTo(ShadingPaints);
        await Assert.That(device.Shadings[0]).IsEqualTo(Expected(space, IccRendering.Default));
        await Assert.That(device.Shadings[1]).IsEqualTo(Expected(space, new(IccIntent.Saturation, false)));
    }

    /// <summary>An image converts with the graphics state's black point choice and its own /Intent.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ImagesFollowTheRenderingAndTheirIntent()
    {
        var profile = BlackPointCompensationTests.CmykProfile();
        var space = BlackPointCompensationTests.IccSpace(profile, CmykComponents);
        var on = RenderImage(profile, string.Empty, string.Empty);
        var off = RenderImage(profile, "/Off gs", string.Empty);
        var twoTables = TwoTableProfile();
        var perceptual = RenderImage(twoTables, string.Empty, "/Intent /Perceptual");
        var twoTableSpace = BlackPointCompensationTests.IccSpace(twoTables, CmykComponents);

        await RenderCheck.Near(on, Middle, Middle, ImageColor(space, IccRendering.Default), ImageTolerance, nameof(ImagesFollowTheRenderingAndTheirIntent));
        await RenderCheck.Near(off, Middle, Middle, ImageColor(space, new(IccIntent.Saturation, false)), ImageTolerance, nameof(ImagesFollowTheRenderingAndTheirIntent));
        await RenderCheck.NotNear(off, Middle, Middle, ImageColor(space, IccRendering.Default), ImageTolerance, nameof(ImagesFollowTheRenderingAndTheirIntent));
        await RenderCheck.Near(perceptual, Middle, Middle, ImageColor(twoTableSpace, new(IccIntent.Perceptual, true)), ImageTolerance, nameof(ImagesFollowTheRenderingAndTheirIntent));
    }

    /// <summary>Builds a CMYK profile whose perceptual table is darker than its colorimetric table.</summary>
    /// <returns>The profile bytes.</returns>
    private static byte[] TwoTableProfile() => Profile(
        Version2,
        "CMYK",
        "Lab ",
        new Tag("A2B0", Lut8(CmykComponents, CornerGrid, static _ => [DarkLightness, NeutralChroma, NeutralChroma])),
        new Tag("A2B1", Lut8(CmykComponents, CornerGrid, static _ => [LightLightness, NeutralChroma, NeutralChroma])));

    /// <summary>Gets the rich black as a fill colour.</summary>
    /// <param name="space">The ICC space.</param>
    /// <param name="rendering">The rendering.</param>
    /// <returns>The colour as 0xRRGGBB.</returns>
    private static uint Expected(PdfColorSpace space, IccRendering rendering) => ColorState.Resolve(space, RichBlack, rendering).Rgb;

    /// <summary>Gets the rich black as an image row converts it.</summary>
    /// <param name="space">The ICC space.</param>
    /// <param name="rendering">The rendering.</param>
    /// <returns>The colour.</returns>
    private static Rgb ImageColor(PdfColorSpace space, IccRendering rendering)
    {
        var bgra = new byte[PixelConverter.BytesPerPixel];
        space.ForRendering(rendering).ConvertRow([byte.MaxValue, byte.MaxValue, byte.MaxValue, byte.MaxValue], bgra, 1);
        return new(bgra[RedByte], bgra[1], bgra[0]);
    }

    /// <summary>Checks the intent and black point choice of a recorded paint.</summary>
    /// <param name="paint">The paint.</param>
    /// <param name="intent">The expected intent.</param>
    /// <param name="mode">The expected black point choice.</param>
    /// <returns>A task.</returns>
    private static async Task AssertState(PaintRecord paint, IccIntent intent, BlackPointCompensationMode mode)
    {
        await Assert.That(paint.Intent).IsEqualTo(intent);
        await Assert.That(paint.Mode).IsEqualTo(mode);
    }

    /// <summary>Runs content with the test graphics states and no colour space.</summary>
    /// <param name="content">The content.</param>
    /// <returns>The device.</returns>
    private static RecordingDevice Run(string content) => Run(content, null, "1.7");

    /// <summary>Runs content with the test graphics states, an ICC CMYK space /CS0 and a shading /Sh0 in it.</summary>
    /// <param name="content">The content.</param>
    /// <param name="profile">The profile of /CS0, or <see langword="null"/> for none.</param>
    /// <param name="version">The header version.</param>
    /// <returns>The device.</returns>
    private static RecordingDevice Run(string content, byte[]? profile, string version)
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = content, Version = version };
        var font = pdf.AddObject("<< /Type /Font /Subtype /Type1 /BaseFont /Test >>");
        var resources = string.Create(CultureInfo.InvariantCulture, $"{GraphicsStates} /Font << /F1 {font} 0 R >>");
        if (profile is not null)
        {
            var icc = pdf.AddStream("/N 4", profile);
            resources += string.Create(CultureInfo.InvariantCulture, $" /ColorSpace << /CS0 [/ICCBased {icc} 0 R] >> {Shading}");
        }

        pdf.Resources = resources;
        using var document = PdfDocumentReader.OpenWith(pdf.ToBytes(), new PdfOpenOptions { FontFactory = static dictionary => new SquareFont(dictionary) });
        var device = new RecordingDevice();
        using (var interpreter = new ContentInterpreter(PdfDocumentRendering.GetRenderCache(document), device, 0))
        {
            ContentExecution.RunPage(interpreter, PdfDocumentPages.GetPage(document, 0));
        }

        return device;
    }

    /// <summary>Renders a one-pixel rich black ICC CMYK image over the page.</summary>
    /// <param name="profile">The image's profile.</param>
    /// <param name="before">Content run before the image is drawn.</param>
    /// <param name="imageEntries">Extra image dictionary entries.</param>
    /// <returns>The rendered page.</returns>
    private static RenderedImage RenderImage(byte[] profile, string before, string imageEntries)
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = string.Create(CultureInfo.InvariantCulture, $"{before} q {Size} 0 0 {Size} 0 0 cm /Im Do Q"), Version = "2.0" };
        var icc = pdf.AddStream("/N 4", profile);
        var image = pdf.AddStream(
            string.Create(
                CultureInfo.InvariantCulture,
                $"/Type /XObject /Subtype /Image /Width 1 /Height 1 /ColorSpace [/ICCBased {icc} 0 R] /BitsPerComponent 8 /Filter /ASCIIHexDecode {imageEntries}"),
            "FFFFFFFF>");
        pdf.Resources = string.Create(CultureInfo.InvariantCulture, $"{GraphicsStates} /XObject << /Im {image} 0 R >>");
        using var page = new RenderTestPage(pdf.ToBytes());
        return page.RenderPage();
    }

    /// <summary>The state of one paint.</summary>
    /// <param name="Intent">The rendering intent.</param>
    /// <param name="Mode">The black point choice.</param>
    /// <param name="Rendering">The ICC conversion they select.</param>
    /// <param name="Fill">The fill colour.</param>
    /// <param name="Stroke">The stroke colour.</param>
    private readonly record struct PaintRecord(IccIntent Intent, BlackPointCompensationMode Mode, IccRendering Rendering, uint Fill, uint Stroke)
    {
        /// <summary>Records a graphics state.</summary>
        /// <param name="state">The state.</param>
        /// <returns>The record.</returns>
        internal static PaintRecord Of(GraphicsState state) =>
            new(state.RenderingIntent, state.BlackPointMode, state.ColorRendering, state.Fill.Rgb, state.Stroke.Rgb);
    }

    /// <summary>A device that records the graphics state of every fill, stroke and glyph, and the colour of every shading.</summary>
    [DebuggerDisplay("RecordingDevice: {Paints.Count} paints")]
    private sealed class RecordingDevice : IContentDevice
    {
        /// <summary>Gets the paints in order.</summary>
        internal List<PaintRecord> Paints { get; } = [];

        /// <summary>Gets the rich black of each shading, as 0xRRGGBB.</summary>
        internal List<uint> Shadings { get; } = [];

        /// <summary>Gets the glyphs drawn.</summary>
        internal int Glyphs { get; private set; }

        /// <inheritdoc/>
        public void Fill(PdfPath path, bool evenOdd, ref GraphicsState state) => Paints.Add(PaintRecord.Of(state));

        /// <inheritdoc/>
        public void Stroke(PdfPath path, ref GraphicsState state) => Paints.Add(PaintRecord.Of(state));

        /// <inheritdoc/>
        public void DrawGlyph(in GlyphEvent glyph, ref GraphicsState state)
        {
            Glyphs++;
            Paints.Add(PaintRecord.Of(state));
        }

        /// <inheritdoc/>
        public void PaintShading(PdfShading shading, ref GraphicsState state) => Shadings.Add(ColorState.Resolve(shading.ColorSpace, RichBlack).Rgb);

        /// <inheritdoc/>
        public void Save()
        {
            // The device keeps no clip.
        }

        /// <inheritdoc/>
        public void Restore()
        {
            // The device keeps no clip.
        }

        /// <inheritdoc/>
        public void Clip(PdfPath path, bool evenOdd, Matrix3x2 ctm)
        {
            // The device keeps no clip.
        }

        /// <inheritdoc/>
        public void DrawImage(IPdfRenderImage image, bool isMask, bool smooth, ref GraphicsState state)
        {
            // Images are checked through a real render.
        }

        /// <inheritdoc/>
        public void BeginGroup(in GroupInfo group)
        {
            // The content has no groups.
        }

        /// <inheritdoc/>
        public void EndGroup(in GroupInfo group)
        {
            // The content has no groups.
        }

        /// <inheritdoc/>
        public void BeginMarkedContent(ReadOnlySpan<byte> tag, PdfDictionary? properties)
        {
            // Marked content does not change colour.
        }

        /// <inheritdoc/>
        public void EndMarkedContent()
        {
            // Marked content does not change colour.
        }

        /// <inheritdoc/>
        public IPictureDevice CreatePictureDevice(PdfRect cull) => throw new NotSupportedException("The test content records no pictures.");
    }
}
