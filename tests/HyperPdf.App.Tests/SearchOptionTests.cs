// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.ViewModels;
using HyperPdf.TestAssets;

namespace HyperPdf.App.Tests;

/// <summary>Checks that the find bar's accent and look-alike character options change what a search finds.</summary>
public sealed class SearchOptionTests
{
    /// <summary>Verifies that "resume" finds nothing until accents are ignored, then finds "résumé".</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task IgnoreDiacritics_ChangesWhatTheFindBarFinds()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("accents.pdf", TextSearchSamples.CreateAccentedPage())]);
        var tab = main.SelectedTab!;
        tab.Search.Query = "resume";
        tab.Search.Refresh();
        _ = await UiWait.UntilAsync(() => !tab.Search.IsSearching);
        var before = tab.Search.Results.Count;

        tab.Search.IgnoreDiacritics = true;
        tab.Search.Refresh();
        _ = await UiWait.UntilAsync(() => !tab.Search.IsSearching && tab.Search.Results.Count == 1);

        await Assert.That(before).IsEqualTo(0);
        await Assert.That(tab.Search.Results.Count).IsEqualTo(1);
    }

    /// <summary>Verifies that a straight quoted word finds the curly quoted word only when look-alike characters match.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FoldCompatibility_ChangesWhatTheFindBarFinds()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("quotes.pdf", TextSearchSamples.CreateAccentedPage())]);
        var tab = main.SelectedTab!;
        tab.Search.Query = "\"quoted\"";
        tab.Search.Refresh();
        _ = await UiWait.UntilAsync(() => !tab.Search.IsSearching);
        var before = tab.Search.Results.Count;

        tab.Search.FoldCompatibility = true;
        tab.Search.Refresh();
        _ = await UiWait.UntilAsync(() => !tab.Search.IsSearching && tab.Search.Results.Count == 1);

        await Assert.That(before).IsEqualTo(0);
        await Assert.That(tab.Search.Results.Count).IsEqualTo(1);
    }
}
