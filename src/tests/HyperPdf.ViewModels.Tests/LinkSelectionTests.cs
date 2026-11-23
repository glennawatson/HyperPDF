// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Geometry;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Tests linking selected text to a web address.</summary>
public sealed class LinkSelectionTests
{
    /// <summary>The pages of the document.</summary>
    private const int Pages = 1;

    /// <summary>The two lines selected, on the first page.</summary>
    private static readonly Dictionary<int, List<PageRect>> Selection = new() { [0] = [new(72, 100, 200, 14), new(72, 120, 150, 14)] };

    /// <summary>One link covers the selected lines and goes to the address typed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinksTheSelectedLinesToTheAddress()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("link.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var before = tab.TryGetDocument()!.GetLinks(0).Length;
        using var ask = tab.Annotations.PromptInteraction.RegisterHandler(static context => context.SetOutput("https://example.org/"));

        _ = await tab.Annotations.LinkSelectionCommand.Execute(Selection).ToTask();
        var links = tab.TryGetDocument()!.GetLinks(0);

        await Assert.That(links.Length).IsEqualTo(before + 1);
        await Assert.That(links[^1].Target.Uri).IsEqualTo("https://example.org/");
    }
}
