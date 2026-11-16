// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks the window that takes hidden information out of the open document.</summary>
public sealed class SanitizeViewModelTests
{
    /// <summary>The pages of the test document.</summary>
    private const int Pages = 2;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 3;

    /// <summary>Accepting the window cleans the document and says what was taken out.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AcceptingCleansTheDocumentAndSaysSo()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("sanitize-accept.pdf", TestPdf.CreateTextFilled(Pages, Lines))]);
        var tab = main.SelectedTab!;
        using var answer = tab.Pages.Sanitize.ShowInteraction.RegisterHandler(static context => context.SetOutput(true));

        _ = await tab.Pages.Sanitize.ShowCommand.Execute().ToTask();

        await Assert.That(tab.Notice).Contains("Removed");
    }

    /// <summary>Closing the window without accepting says nothing and changes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancellingChangesNothing()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("sanitize-cancel.pdf", TestPdf.CreateTextFilled(Pages, Lines))]);
        var tab = main.SelectedTab!;
        using var answer = tab.Pages.Sanitize.ShowInteraction.RegisterHandler(static context => context.SetOutput(false));

        _ = await tab.Pages.Sanitize.ShowCommand.Execute().ToTask();

        await Assert.That(tab.Notice ?? string.Empty).DoesNotContain("Removed");
    }
}
