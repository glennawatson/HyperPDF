// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks the comparison of the open document with another file.</summary>
public sealed class CompareViewModelTests
{
    /// <summary>The pages of the open document.</summary>
    private const int Pages = 2;

    /// <summary>The pages of the document it is compared with.</summary>
    private const int MorePages = 3;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 3;

    /// <summary>The least share of the page the added box must change.</summary>
    private const double MinimumLook = 0.05;

    /// <summary>A document compared with a copy of itself has no differences.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ACopyHasNoDifferences()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("compare-a.pdf", TestPdf.CreateTextFilled(Pages, Lines))]);
        var other = test.CreateDocument("compare-b.pdf", TestPdf.CreateTextFilled(Pages, Lines));
        var compare = main.SelectedTab!.Pages.Compare;
        using var choose = compare.ChooseFileInteraction.RegisterHandler(context => context.SetOutput(other));
        using var show = compare.ShowInteraction.RegisterHandler(static context => context.SetOutput(RxVoid.Default));

        _ = await compare.CompareCommand.Execute().ToTask();

        await Assert.That(compare.Differences.Length).IsEqualTo(0);
    }

    /// <summary>A page with the same words but a large coloured box added is listed as looking different, with no words changed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ADifferentLookIsListedWithNoWordsChanged()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("look-a.pdf", OnePage("BT /F1 24 Tf 20 100 Td (Same words) Tj ET"))]);
        var other = test.CreateDocument("look-b.pdf", OnePage("0 0 1 rg 20 20 160 60 re f 0 g BT /F1 24 Tf 20 100 Td (Same words) Tj ET"));
        var compare = main.SelectedTab!.Pages.Compare;
        using var choose = compare.ChooseFileInteraction.RegisterHandler(context => context.SetOutput(other));
        using var show = compare.ShowInteraction.RegisterHandler(static context => context.SetOutput(RxVoid.Default));

        _ = await compare.CompareCommand.Execute().ToTask();

        await Assert.That(compare.Differences.Length).IsEqualTo(1);
        await Assert.That(compare.Differences[0].Added + compare.Differences[0].Removed).IsEqualTo(0);
        await Assert.That(compare.Differences[0].LookChange).IsGreaterThan(MinimumLook);
    }

    /// <summary>A page that only the other document has is listed with all its words added.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnExtraPageIsListed()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("compare-c.pdf", TestPdf.CreateTextFilled(Pages, Lines))]);
        var other = test.CreateDocument("compare-d.pdf", TestPdf.CreateTextFilled(MorePages, Lines));
        var compare = main.SelectedTab!.Pages.Compare;
        using var choose = compare.ChooseFileInteraction.RegisterHandler(context => context.SetOutput(other));
        using var show = compare.ShowInteraction.RegisterHandler(static context => context.SetOutput(RxVoid.Default));

        _ = await compare.CompareCommand.Execute().ToTask();

        await Assert.That(compare.Differences.Length).IsEqualTo(1);
        await Assert.That(compare.Differences[0].Page).IsEqualTo(Pages);
        await Assert.That(compare.Differences[0].Added).IsGreaterThan(0);
    }

    /// <summary>Makes a one page document from page content, drawn with Helvetica.</summary>
    /// <param name="content">The content stream.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] OnePage(string content) => MiniPdf.Build(
        "<< /Type /Catalog /Pages 2 0 R >>",
        "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 200 200] /Resources 5 0 R /Contents 4 0 R >>",
        MiniPdf.Stream(string.Empty, content),
        "<< /Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >> >> >>");
}
