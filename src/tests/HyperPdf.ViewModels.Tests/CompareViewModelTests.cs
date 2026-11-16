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

        await Assert.That(compare.Differences.Count).IsEqualTo(0);
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

        await Assert.That(compare.Differences.Count).IsEqualTo(1);
        await Assert.That(compare.Differences[0].Page).IsEqualTo(Pages);
        await Assert.That(compare.Differences[0].Added).IsGreaterThan(0);
    }
}
