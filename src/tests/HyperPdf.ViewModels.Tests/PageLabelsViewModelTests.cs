// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Markup;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks the window that numbers a run of pages i, ii, iii or A-1, and the way it keeps the other pages' labels.</summary>
public sealed class PageLabelsViewModelTests
{
    /// <summary>The pages of the test document.</summary>
    private const int Pages = 6;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 3;

    /// <summary>The page the appendix starts on, counted from 0.</summary>
    private const int AppendixStart = 4;

    /// <summary>The page the second range starts on, counted from 0.</summary>
    private const int Third = 2;

    /// <summary>The number the page after the front matter has when numbering carries on.</summary>
    private const int ThirdNumber = 3;

    /// <summary>The number of ranges a change that numbers a run of pages leaves when it needs one before and one after.</summary>
    private const int ThreeRanges = 3;

    /// <summary>The number of ranges a change that numbers the first pages leaves.</summary>
    private const int TwoRanges = 2;

    /// <summary>The last page of the front matter, counted from 0.</summary>
    private const int FrontMatterEnd = 1;

    /// <summary>Numbering the first pages in Roman numerals leaves the rest numbered from 1 again.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FrontMatterIsNumberedInRomanNumeralsAndTheRestCarriesOn()
    {
        var ranges = PageLabelMerger.Apply([], Pages, 0, FrontMatterEnd, PageLabelStyle.LowerRoman, string.Empty, 1);

        await Assert.That(ranges.Count).IsEqualTo(TwoRanges);
        await Assert.That(ranges[0]).IsEqualTo(new(0, PageLabelStyle.LowerRoman, string.Empty, 1));
        await Assert.That(ranges[1]).IsEqualTo(new(Third, PageLabelStyle.Decimal, string.Empty, ThirdNumber));
    }

    /// <summary>Numbering a run in the middle keeps the pages before it as they were and carries on after it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ARunInTheMiddleKeepsThePagesAroundIt()
    {
        var ranges = PageLabelMerger.Apply([], Pages, Third, AppendixStart - 1, PageLabelStyle.UpperLetters, "App ", 1);

        await Assert.That(ranges.Count).IsEqualTo(ThreeRanges);
        await Assert.That(ranges[0]).IsEqualTo(new(0, PageLabelStyle.Decimal, string.Empty, 1));
        await Assert.That(ranges[1]).IsEqualTo(new(Third, PageLabelStyle.UpperLetters, "App ", 1));
        await Assert.That(ranges[Third]).IsEqualTo(new(AppendixStart, PageLabelStyle.Decimal, string.Empty, AppendixStart + 1));
    }

    /// <summary>A range that is already there and starts inside the numbered run is replaced, and the one after it is kept.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RangesInsideTheRunAreReplaced()
    {
        PageLabelRun[] existing =
        [
            new(0, PageLabelStyle.Decimal, string.Empty, 1),
            new(1, PageLabelStyle.LowerRoman, string.Empty, 1),
            new(AppendixStart, PageLabelStyle.Decimal, "B-", 1),
        ];

        var ranges = PageLabelMerger.Apply(existing, Pages, 1, AppendixStart - 1, PageLabelStyle.UpperRoman, string.Empty, 1);

        await Assert.That(ranges.Count).IsEqualTo(ThreeRanges);
        await Assert.That(ranges[1]).IsEqualTo(new(1, PageLabelStyle.UpperRoman, string.Empty, 1));
        await Assert.That(ranges[Third]).IsEqualTo(existing[Third]);
    }

    /// <summary>The window numbers the pages it was asked to and the document reports the new ranges.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AcceptingTheWindowNumbersThePages()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("labels-accept.pdf", TestPdf.CreateTextFilled(Pages, Lines))]);
        var tab = main.SelectedTab!;
        using var answer = tab.Pages.Labels.ShowInteraction.RegisterHandler(static context =>
        {
            context.Input.FromPage = "1";
            context.Input.ToPage = "2";
            context.Input.StyleIndex = (int)PageLabelStyle.LowerRoman;
            context.SetOutput(true);
        });

        _ = await tab.Pages.Labels.ShowCommand.Execute().ToTask();
        var ranges = DocumentFeature<IPageLabeler>.Get(tab.TryGetDocument())!.GetRanges();

        await Assert.That(ranges.Length).IsEqualTo(TwoRanges);
        await Assert.That(ranges[0].Style).IsEqualTo(PageLabelStyle.LowerRoman);
        await Assert.That(ranges[1].StartPage).IsEqualTo(Third);
    }

    /// <summary>Pages that are not in the document are refused with a notice and nothing changes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PagesNotInTheDocumentAreRefused()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("labels-refuse.pdf", TestPdf.CreateTextFilled(Pages, Lines))]);
        var tab = main.SelectedTab!;
        using var answer = tab.Pages.Labels.ShowInteraction.RegisterHandler(static context =>
        {
            context.Input.FromPage = "99";
            context.SetOutput(true);
        });

        _ = await tab.Pages.Labels.ShowCommand.Execute().ToTask();

        await Assert.That(DocumentFeature<IPageLabeler>.Get(tab.TryGetDocument())!.GetRanges().Length).IsEqualTo(0);
    }
}
