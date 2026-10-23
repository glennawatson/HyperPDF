// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>
/// Renders nested transparency groups, the page-level /Group and group blending colour spaces (ISO 32000-2 11.4.5, 11.4.7,
/// 11.6.6). PDFium ignores the page /Group entirely and never reads a group's /CS; these tests follow ISO 32000-2.
/// </summary>
[NotInParallel]
public sealed class GroupNestingRenderTests
{
    /// <summary>The page size in points.</summary>
    private const int Size = 100;

    /// <summary>The largest channel difference accepted.</summary>
    private const int Tolerance = 8;

    /// <summary>A channel at half strength over white.</summary>
    private const int Half = 128;

    /// <summary>A channel at quarter strength: half alpha twice over white.</summary>
    private const int Quarter = 64;

    /// <summary>A channel at three quarters: half of full plus half of half.</summary>
    private const int ThreeQuarters = 192;

    /// <summary>A device coordinate inside both overlapping squares.</summary>
    private const int Overlap = 50;

    /// <summary>The device column inside only the first square.</summary>
    private const int FirstOnlyColumn = 20;

    /// <summary>The device row inside only the first square.</summary>
    private const int FirstOnlyRow = 80;

    /// <summary>The device column of the CMYK overlap test, inside both squares.</summary>
    private const int CmykOverlap = 50;

    /// <summary>A device column outside the inner square but inside the page.</summary>
    private const int EdgeColumn = 10;

    /// <summary>The groups the nested group tests draw.</summary>
    private const int NestedGroups = 2;

    /// <summary>The page content that draws the outer form.</summary>
    private const string DrawOuter = "/Outer Do";

    /// <summary>The form content that draws the inner form.</summary>
    private const string DrawInner = "/Inner Do";

    /// <summary>Content that paints a blue square.</summary>
    private const string BlueSquare = "0 0 1 rg 20 20 60 60 re f";

    /// <summary>The most a channel may be in a dark pixel.</summary>
    private const int Dark = 40;

    /// <summary>The blue channel of cyan times magenta ink after the device conversion to RGB.</summary>
    private const int DarkBlue = 131;

    /// <summary>Two overlapping squares at half alpha: red then blue.</summary>
    private const string TwoSquares = "/Half gs 1 0 0 rg 10 10 50 50 re f 0 0 1 rg 40 40 50 50 re f";

    /// <summary>The half alpha graphics state.</summary>
    private const string HalfState = "/Half << /Type /ExtGState /ca 0.5 /CA 0.5 >>";

    /// <summary>The Multiply graphics state.</summary>
    private const string MultiplyState = "/Mul << /Type /ExtGState /BM /Multiply >>";

    /// <summary>The group attributes shared by the form XObjects.</summary>
    private const string FormHead = "/Type /XObject /Subtype /Form /BBox [0 0 100 100]";

    /// <summary>An inner knockout group inside a plain outer group still knocks its own objects out.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InnerKnockoutGroupKnocksOutInsideAPlainOuterGroup()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = DrawOuter };
        var inner = pdf.AddStream($"{FormHead} /Group << /S /Transparency /I true /K true >>", TwoSquares);
        var outer = pdf.AddStream($"{FormHead} /Group << /S /Transparency /I true >> /Resources << /ExtGState << {HalfState} >> /XObject << /Inner {inner} 0 R >> >>", DrawInner);
        pdf.Resources = $"/XObject << /Outer {outer} 0 R >>";

        var image = Render(pdf);

        await RenderCheck.Near(image, Overlap, Overlap, new(Half, Half, Rgb.Blue255.Blue), Tolerance, nameof(InnerKnockoutGroupKnocksOutInsideAPlainOuterGroup));
        await RenderCheck.Near(image, FirstOnlyColumn, FirstOnlyRow, new(Rgb.Red255.Red, Half, Half), Tolerance, nameof(InnerKnockoutGroupKnocksOutInsideAPlainOuterGroup));
    }

    /// <summary>A plain inner group inside a knockout outer group still composites its own objects over each other.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task InnerPlainGroupCompositesInsideAKnockoutOuterGroup()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = DrawOuter };
        var inner = pdf.AddStream($"{FormHead} /Group << /S /Transparency /I true >>", TwoSquares);
        var outer = pdf.AddStream($"{FormHead} /Group << /S /Transparency /I true /K true >> /Resources << /ExtGState << {HalfState} >> /XObject << /Inner {inner} 0 R >> >>", DrawInner);
        pdf.Resources = $"/XObject << /Outer {outer} 0 R >>";

        var image = Render(pdf);

        await RenderCheck.Near(image, Overlap, Overlap, new(Half, Quarter, ThreeQuarters), Tolerance, nameof(InnerPlainGroupCompositesInsideAKnockoutOuterGroup));
    }

    /// <summary>A group is one object of its knockout parent, so it replaces what the parent drew before it (11.4.6).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GroupInsideAKnockoutGroupReplacesEarlierObjects()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = DrawOuter };
        var inner = pdf.AddStream($"{FormHead} /Group << /S /Transparency /I true >>", BlueSquare);
        var outer = pdf.AddStream(
            $"{FormHead} /Group << /S /Transparency /I true /K true >> /Resources << /ExtGState << {HalfState} >> /XObject << /Inner {inner} 0 R >> >>",
            "1 0 0 rg 0 0 100 100 re f /Half gs /Inner Do");
        pdf.Resources = $"/XObject << /Outer {outer} 0 R >>";

        var image = Render(pdf);

        await RenderCheck.Near(image, Overlap, Overlap, new(Half, Half, Rgb.Blue255.Blue), Tolerance, nameof(GroupInsideAKnockoutGroupReplacesEarlierObjects));
        await RenderCheck.Near(image, EdgeColumn, FirstOnlyRow, new(Rgb.Red255.Red, 0, 0), Tolerance, nameof(GroupInsideAKnockoutGroupReplacesEarlierObjects));
    }

    /// <summary>A non-isolated group inside an isolated group sees only the parent's content, not the page beneath it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NonIsolatedGroupInsideAnIsolatedGroupIgnoresThePage()
    {
        var image = RenderNestedMultiply(parentIsolated: true);

        await RenderCheck.Near(image, Overlap, Overlap, Rgb.Blue255, Tolerance, nameof(NonIsolatedGroupInsideAnIsolatedGroupIgnoresThePage));
    }

    /// <summary>A non-isolated group inside a non-isolated group blends with the page beneath both.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NonIsolatedGroupInsideANonIsolatedGroupBlendsWithThePage()
    {
        var image = RenderNestedMultiply(parentIsolated: false);

        await RenderCheck.Near(image, Overlap, Overlap, Rgb.Black, Tolerance, nameof(NonIsolatedGroupInsideANonIsolatedGroupBlendsWithThePage));
    }

    /// <summary>A page /Group with /K true makes the page's top-level objects knock each other out (11.4.7).</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KnockoutPageGroupKnocksOutTopLevelObjects()
    {
        var image = RenderPage("/Group << /S /Transparency /K true >>", TwoSquares);

        await RenderCheck.Near(image, Overlap, Overlap, new(Half, Half, Rgb.Blue255.Blue), Tolerance, nameof(KnockoutPageGroupKnocksOutTopLevelObjects));
        await RenderCheck.Near(image, FirstOnlyColumn, FirstOnlyRow, new(Rgb.Red255.Red, Half, Half), Tolerance, nameof(KnockoutPageGroupKnocksOutTopLevelObjects));
    }

    /// <summary>A page /Group without /K composites top-level objects normally, whatever /I says.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NonKnockoutPageGroupCompositesTopLevelObjects()
    {
        var image = RenderPage("/Group << /S /Transparency /I false /CS /DeviceRGB >>", TwoSquares);

        await RenderCheck.Near(image, Overlap, Overlap, new(Half, Quarter, ThreeQuarters), Tolerance, nameof(NonKnockoutPageGroupCompositesTopLevelObjects));
    }

    /// <summary>Opaque painting looks the same with and without a page group that names a CMYK blending space.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageGroupColourSpaceLeavesOpaquePaintingUnchanged()
    {
        const string Content = "1 0 0 rg 10 10 50 50 re f 0 0 1 rg 40 40 50 50 re f";
        var plain = RenderPage(string.Empty, Content);
        var grouped = RenderPage("/Group << /S /Transparency /CS /DeviceCMYK >>", Content);

        await RenderCheck.Near(grouped, Overlap, Overlap, Rgb.Blue255, Tolerance, nameof(PageGroupColourSpaceLeavesOpaquePaintingUnchanged));
        await Assert.That(grouped.Pixels.AsSpan().SequenceEqual(plain.Pixels)).IsTrue();
    }

    /// <summary>
    /// A DeviceCMYK group blends its components as complements (11.3.5.1), so Multiply of cyan over magenta ink gives
    /// the same dark blue an RGB group gives. PDFium never reads /CS and always blends in RGB.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CmykGroupMultiplyMatchesTheComplementRule()
    {
        const string Content = "/Mul gs 1 0 0 0 k 10 10 50 50 re f 0 1 0 0 k 40 40 50 50 re f";
        var cmyk = RenderCmykGroup("/DeviceCMYK", Content);
        var rgb = RenderCmykGroup("/DeviceRGB", Content);

        await RenderCheck.Near(cmyk, CmykOverlap, CmykOverlap, new(0, 0, DarkBlue), Dark, nameof(CmykGroupMultiplyMatchesTheComplementRule));
        await Assert.That(cmyk.Pixels.AsSpan().SequenceEqual(rgb.Pixels)).IsTrue();
    }

    /// <summary>The device is told a group's /CS, and a nested group without one inherits it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GroupsReportAndInheritTheirBlendingColourSpace()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = DrawOuter };
        var inner = pdf.AddStream($"{FormHead} /Group << /S /Transparency /K true >>", BlueSquare);
        var outer = pdf.AddStream($"{FormHead} /Group << /S /Transparency /I true /CS /DeviceCMYK >> /Resources << /XObject << /Inner {inner} 0 R >> >>", DrawInner);
        pdf.Resources = $"/XObject << /Outer {outer} 0 R >>";

        var groups = GraphicsStateRecorder.Run(pdf).Groups;

        await Assert.That(groups.Count).IsEqualTo(NestedGroups);
        await Assert.That(groups[0].BlendSpace?.Kind).IsEqualTo(PdfColorSpaceKind.DeviceCmyk);
        await Assert.That(groups[1].BlendSpace?.Kind).IsEqualTo(PdfColorSpaceKind.DeviceCmyk);
    }

    /// <summary>A non-isolated group may not name its own space, and a Lab space is refused for any group.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnusableGroupColourSpacesAreIgnored()
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/Plain Do /Lab Do" };
        var plain = pdf.AddStream($"{FormHead} /Group << /S /Transparency /K true /CS /DeviceCMYK >>", BlueSquare);
        var lab = pdf.AddStream($"{FormHead} /Group << /S /Transparency /I true /CS [/Lab << /WhitePoint [0.95 1 1.09] >>] >>", BlueSquare);
        pdf.Resources = $"/XObject << /Plain {plain} 0 R /Lab {lab} 0 R >>";

        var groups = GraphicsStateRecorder.Run(pdf).Groups;

        await Assert.That(groups.Count).IsEqualTo(NestedGroups);
        await Assert.That(groups[0].BlendSpace).IsNull();
        await Assert.That(groups[1].BlendSpace).IsNull();
    }

    /// <summary>Renders a page whose content draws a group that holds a non-isolated Multiply group.</summary>
    /// <param name="parentIsolated">Whether the outer group is isolated.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage RenderNestedMultiply(bool parentIsolated)
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "1 0 0 rg 0 0 100 100 re f /Outer Do" };
        var inner = pdf.AddStream($"{FormHead} /Group << /S /Transparency /I false >> /Resources << /ExtGState << {MultiplyState} >> >>", "/Mul gs 0 0 1 rg 20 20 60 60 re f");
        var outer = pdf.AddStream(
            $"{FormHead} /Group << /S /Transparency /I {(parentIsolated ? "true" : "false")} >> /Resources << /XObject << /Inner {inner} 0 R >> >>",
            DrawInner);
        pdf.Resources = $"/XObject << /Outer {outer} 0 R >>";
        return Render(pdf);
    }

    /// <summary>Renders a page that draws content with extra page dictionary entries.</summary>
    /// <param name="entries">The page entries, such as /Group.</param>
    /// <param name="content">The page content.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage RenderPage(string entries, string content) =>
        Render(new(Size, Size) { Content = content, PageEntries = entries, Resources = $"/ExtGState << {HalfState} {MultiplyState} >>" });

    /// <summary>Renders an isolated group with a blending colour space.</summary>
    /// <param name="space">The /CS value.</param>
    /// <param name="content">The group content.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage RenderCmykGroup(string space, string content)
    {
        var pdf = new RenderTestPdf(Size, Size) { Content = "/Fm Do" };
        var form = pdf.AddStream($"{FormHead} /Group << /S /Transparency /I true /CS {space} >> /Resources << /ExtGState << {MultiplyState} >> >>", content);
        pdf.Resources = $"/XObject << /Fm {form} 0 R >>";
        return Render(pdf);
    }

    /// <summary>Renders a prepared document's first page.</summary>
    /// <param name="pdf">The document.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage Render(RenderTestPdf pdf)
    {
        using var page = new RenderTestPage(pdf.ToBytes());
        return page.RenderPage();
    }
}
