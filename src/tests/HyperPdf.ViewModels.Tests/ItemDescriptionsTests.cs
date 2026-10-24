// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks the words a screen reader says for list items, whichever toolkit shows them.</summary>
public sealed class ItemDescriptionsTests
{
    /// <summary>The page the page tests describe, counting from zero.</summary>
    private const int ThirdPage = 2;

    /// <summary>The pages in the document the page tests describe.</summary>
    private const int LongDocument = 40;

    /// <summary>The position of the item the position tests describe, counting from zero.</summary>
    private const int SecondItem = 1;

    /// <summary>The items in the list the position tests describe.</summary>
    private const int ListLength = 7;

    /// <summary>The file name the tab tests describe.</summary>
    private const string FileName = "reader.pdf";

    /// <summary>A page whose label is its number is described by the number.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlainPageLabelIsNotRepeated()
    {
        var text = ItemDescriptions.DescribePage("3", ThirdPage, LongDocument);

        await Assert.That(text).IsEqualTo("Page 3 of 40");
    }

    /// <summary>A page with its own label says both the label and the number.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LabelledPageSaysBoth()
    {
        var text = ItemDescriptions.DescribePage("iii", ThirdPage, LongDocument);

        await Assert.That(text).IsEqualTo("Page iii, 3 of 40");
    }

    /// <summary>A tab with unsaved changes says so.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnsavedTabSaysSo()
    {
        await Assert.That(ItemDescriptions.DescribeTab(FileName, true)).IsEqualTo("reader.pdf, unsaved changes");
        await Assert.That(ItemDescriptions.DescribeTab(FileName, false)).IsEqualTo(FileName);
    }

    /// <summary>An item's place is spoken as its number out of the total.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PositionIsNumberOutOfTotal()
    {
        var text = ItemDescriptions.DescribePosition("an item", SecondItem, ListLength);

        await Assert.That(text).IsEqualTo("2 of 7");
    }

    /// <summary>An empty list gives no position.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmptyListHasNoPosition() =>
        await Assert.That(ItemDescriptions.DescribePosition("an item", 0, 0)).IsNull();

    /// <summary>A plain text item names itself, and an unknown item is numbered.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextNamesItselfAndUnknownItemsAreNumbered()
    {
        object unknown = new();

        await Assert.That(ItemDescriptions.Describe("Cyan", 0, ListLength)).IsEqualTo("Cyan");
        await Assert.That(ItemDescriptions.Describe(unknown, SecondItem, ListLength)).IsEqualTo("Item 2 of 7");
        await Assert.That(ItemDescriptions.Describe(unknown, 0, 0)).IsNull();
    }

    /// <summary>With no document open the reader is described as having none, and a missing certificate as empty.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NothingOpenIsDescribedAsSuch()
    {
        await Assert.That(ItemDescriptions.DescribeReader(null)).IsEqualTo("No document");
        await Assert.That(ItemDescriptions.DescribeSelection(null)).IsEqualTo(string.Empty);
        await Assert.That(ItemDescriptions.DescribeCertificate(null)).IsEqualTo(string.Empty);
    }

    /// <summary>A loaded document is described by the page the reader is on.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OpenDocumentIsDescribedByPage()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("describe.pdf", ListLength)]);

        var text = ItemDescriptions.DescribeReader(main.SelectedTab);

        await Assert.That(text).IsEqualTo("Page 1 of 7");
    }
}
