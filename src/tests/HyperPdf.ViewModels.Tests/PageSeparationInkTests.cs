// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks that the inks of a colour separated page can be chosen to show the page that holds each.</summary>
public sealed class PageSeparationInkTests
{
    /// <summary>The place of the second ink among the inks of the page.</summary>
    private const int SecondInk = 1;

    /// <summary>Choosing an ink selects it, which asks the tab to show the page that holds it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChoosingAnInkShowsItsPage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("inks.pdf", SeparationPdf.CreatePreseparated())]);
        var inks = main.SelectedTab!.Separations;

        await Assert.That(inks.HasSeparations).IsTrue();
        await Assert.That(inks.Colorants.Count).IsGreaterThan(SecondInk);

        _ = await inks.ShowInkCommand.Execute(SecondInk).ToTask();

        await Assert.That(inks.SelectedIndex).IsEqualTo(SecondInk);
    }
}
