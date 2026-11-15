// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Reading;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Markup;
using HyperPdf.ViewModels.Viewing;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks the window and the work that add watermarks, headers, footers, page and Bates numbers, backgrounds and crops.</summary>
public sealed class PageDecorationViewModelTests
{
    /// <summary>The pages of the test document.</summary>
    private const int Pages = 3;

    /// <summary>The lines of text on each page.</summary>
    private const int Lines = 4;

    /// <summary>What a crop takes off each side, in points.</summary>
    private const decimal CropAmount = 36;

    /// <summary>The width and height a crop takes off a page when it takes the same off every side, in points.</summary>
    private const float Both = 72;

    /// <summary>How close two sizes in points are for them to be equal.</summary>
    private const float Tolerance = 0.5F;

    /// <summary>The place of the "pages I type" choice among the scopes.</summary>
    private const int TypedScope = 2;

    /// <summary>The number of a page that is not in the document.</summary>
    private const string FarPage = "99";

    /// <summary>The page numbers are written on the pages when the window is accepted.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PageNumbersAreWrittenWhenAccepted()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("decorate-numbers.pdf", TestPdf.CreateTextFilled(Pages, Lines))]);
        var tab = main.SelectedTab!;
        using var answer = tab.Decorations.ShowInteraction.RegisterHandler(static context => context.SetOutput(true));

        await tab.Decorations.ShowAsync(PageDecorationKind.PageNumbers);

        await Assert.That(TextOf(tab, 1)).Contains("Page 2 of 3");
    }

    /// <summary>Closing the window without accepting changes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancellingChangesNothing()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("decorate-cancel.pdf", TestPdf.CreateTextFilled(Pages, Lines))]);
        var tab = main.SelectedTab!;
        using var answer = tab.Decorations.ShowInteraction.RegisterHandler(static context => context.SetOutput(false));

        await tab.Decorations.ShowAsync(PageDecorationKind.PageNumbers);

        await Assert.That(TextOf(tab, 1)).DoesNotContain("Page 2 of 3");
    }

    /// <summary>Pages that are not in the document are refused with a notice, and nothing changes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PagesNotInTheDocumentAreRefused()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("decorate-range.pdf", TestPdf.CreateTextFilled(Pages, Lines))]);
        var tab = main.SelectedTab!;
        using var answer = tab.Decorations.ShowInteraction.RegisterHandler(static context =>
        {
            context.Input.ScopeIndex = TypedScope;
            context.Input.RangeText = FarPage;
            context.SetOutput(true);
        });

        await tab.Decorations.ShowAsync(PageDecorationKind.Watermark);

        await Assert.That(tab.Notice).Contains("not in the document");
        await Assert.That(TextOf(tab, 0)).DoesNotContain("CONFIDENTIAL");
    }

    /// <summary>A crop makes the pages smaller by the margins taken off.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CropMakesThePagesSmaller()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("decorate-crop.pdf", TestPdf.CreateTextFilled(Pages, Lines))]);
        var tab = main.SelectedTab!;
        var before = tab.Source.PageSizes[0];
        using var answer = tab.Decorations.ShowInteraction.RegisterHandler(static context =>
        {
            var model = context.Input;
            model.CropLeft = CropAmount;
            model.CropTop = CropAmount;
            model.CropRight = CropAmount;
            model.CropBottom = CropAmount;
            context.SetOutput(true);
        });

        await tab.Decorations.ShowAsync(PageDecorationKind.Crop);

        var after = tab.Source.PageSizes[0];
        await Assert.That(after.Width).IsBetween(before.Width - Both - Tolerance, before.Width - Both + Tolerance);
        await Assert.That(after.Height).IsBetween(before.Height - Both - Tolerance, before.Height - Both + Tolerance);
    }

    /// <summary>Reads the words of a page.</summary>
    /// <param name="tab">The tab.</param>
    /// <param name="page">The zero based page.</param>
    /// <returns>The page's characters as text.</returns>
    private static string TextOf(DocumentTabViewModel tab, int page)
    {
        var characters = new List<PageCharacter>();
        DocumentFeature<ITextLayoutSource>.Get(tab.TryGetDocument())!.GetCharacters(page, characters);
        var text = new StringBuilder(characters.Count);
        foreach (var character in characters)
        {
            _ = text.Append(character.Value);
        }

        return text.ToString();
    }
}
