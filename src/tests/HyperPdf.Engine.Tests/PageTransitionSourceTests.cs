// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Presentation;
using HyperPdf.Core.Printing;
using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks the transitions and advance times pages ask for are read, and kept when pages are copied (ISO 32000-2 §12.4.4).</summary>
public sealed class PageTransitionSourceTests
{
    /// <summary>The seconds the first page's effect lasts.</summary>
    private const double WipeSeconds = 2;

    /// <summary>The seconds the first page stays.</summary>
    private const double WipeStay = 5;

    /// <summary>The seconds the second page's effect lasts.</summary>
    private const double SplitSeconds = 0.5;

    /// <summary>The seconds the third page stays.</summary>
    private const double PlainStay = 7;

    /// <summary>The direction of the first page's wipe, in degrees.</summary>
    private const int WipeDirection = 90;

    /// <summary>The page with a wipe.</summary>
    private const int WipePage = 0;

    /// <summary>The page with a split.</summary>
    private const int SplitPage = 1;

    /// <summary>The page with only an advance time.</summary>
    private const int PlainPage = 2;

    /// <summary>The page with neither.</summary>
    private const int BarePage = 3;

    /// <summary>Gets a document of four pages: a wipe that stays 5 seconds, a split, a page with only an advance time, and a bare page.</summary>
    private static byte[] Slides => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R 4 0 R 5 0 R 6 0 R] /Count 4 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Dur 5 /Trans << /Type /Trans /S /Wipe /D 2 /Di 90 >> >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Trans << /S /Split /D 0.5 /Dm /V /M /O >> >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Dur 7 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] >>");

    /// <summary>Each page gives the style, time, direction and advance it asked for.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsWhatEachPageAsksFor()
    {
        using var test = new HyperPdfTestDocument(Slides);
        var source = DocumentFeature<IPageTransitionSource>.Cast(test.Document)!;

        var wipe = source.GetTransition(WipePage);
        var split = source.GetTransition(SplitPage);
        var plain = source.GetTransition(PlainPage);

        await Assert.That(wipe).IsNotNull();
        await Assert.That(wipe!.Style).IsEqualTo(TransitionStyle.Wipe);
        await Assert.That(wipe.DurationSeconds).IsEqualTo(WipeSeconds);
        await Assert.That(wipe.Direction).IsEqualTo(WipeDirection);
        await Assert.That(wipe.AdvanceAfterSeconds).IsEqualTo(WipeStay);
        await Assert.That(split!.Style).IsEqualTo(TransitionStyle.Split);
        await Assert.That(split.DurationSeconds).IsEqualTo(SplitSeconds);
        await Assert.That(split.Vertical).IsTrue();
        await Assert.That(split.Inward).IsFalse();
        await Assert.That(split.AdvanceAfterSeconds).IsNull();
        await Assert.That(plain!.Style).IsEqualTo(TransitionStyle.Replace);
        await Assert.That(plain.AdvanceAfterSeconds).IsEqualTo(PlainStay);
        await Assert.That(source.GetTransition(BarePage)).IsNull();
        await Assert.That(source.GetTransition(Slides.Length)).IsNull();
    }

    /// <summary>Copying pages to a new file keeps their transitions and advance times.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CopiedPagesKeepTheirTransitions()
    {
        var source = Path.Combine(Path.GetTempPath(), $"hyperpdf-slides-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(source, Slides);
        try
        {
            using var run = ExportRun.Create(source, [SplitPage, WipePage, PlainPage], SheetLayout.Default);
            var copy = DocumentFeature<IPageTransitionSource>.Cast(run.Output!)!;

            await Assert.That(run.Written).IsTrue();
            await Assert.That(copy.GetTransition(0)?.Style).IsEqualTo(TransitionStyle.Split);
            await Assert.That(copy.GetTransition(1)?.Style).IsEqualTo(TransitionStyle.Wipe);
            await Assert.That(copy.GetTransition(1)?.AdvanceAfterSeconds).IsEqualTo(WipeStay);
            await Assert.That(copy.GetTransition(PlainPage)?.AdvanceAfterSeconds).IsEqualTo(PlainStay);
        }
        finally
        {
            File.Delete(source);
        }
    }
}
