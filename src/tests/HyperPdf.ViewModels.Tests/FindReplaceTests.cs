// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels.Viewing;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Tests finding words in the pages' own text and putting others in their place.</summary>
public sealed class FindReplaceTests
{
    /// <summary>The pages of the text sample.</summary>
    private const int Pages = 2;

    /// <summary>The lines on each page of the text sample.</summary>
    private const int Lines = 2;

    /// <summary>Every place the words are on every page has the new words, and the old words are gone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReplacesTheWordsOnEveryPage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("replace.pdf", TestPdf.CreateTextFilled(Pages, Lines))]);
        var tab = main.SelectedTab!;
        using var window = tab.FindReplaceInteraction.RegisterHandler(static context =>
        {
            context.Input.Find = "brown fox";
            context.Input.Replacement = "red hen";
            context.SetOutput(true);
        });

        _ = await tab.FindAndReplaceCommand.Execute().ToTask();
        var text = await PageText(tab, Pages);

        await Assert.That(text).Contains("red hen");
        await Assert.That(text).DoesNotContain("brown fox");
    }

    /// <summary>With Match case on, words written with other capitals are left alone.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MatchCaseLeavesOtherCapitalsAlone()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("case.pdf", TestPdf.CreateTextFilled(1, Lines))]);
        var tab = main.SelectedTab!;
        using var window = tab.FindReplaceInteraction.RegisterHandler(static context =>
        {
            context.Input.Find = "BROWN";
            context.Input.Replacement = "red";
            context.Input.MatchCase = true;
            context.SetOutput(true);
        });

        _ = await tab.FindAndReplaceCommand.Execute().ToTask();

        await Assert.That(await PageText(tab, 1)).Contains("brown");
    }

    /// <summary>Reads the text of every text object on the first pages.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="pages">How many pages to read.</param>
    /// <returns>The text, one object after another.</returns>
    private static async Task<string> PageText(DocumentTabViewModel tab, int pages)
    {
        var editor = DocumentFeature<IPageContentEditor>.Get(tab.TryGetDocument())!;
        var all = new List<string>();
        for (var page = 0; page < pages; page++)
        {
            foreach (var item in await editor.ReadObjects(page).ToTask())
            {
                all.Add(item.Text);
            }
        }

        return string.Join('\n', all);
    }
}
