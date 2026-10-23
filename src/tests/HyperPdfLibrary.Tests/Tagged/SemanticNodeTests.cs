// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Structure.Tagged;

namespace HyperPdfLibrary.Tests.Tagged;

/// <summary>Tests for the reading nodes built from the tags: order, roles, tables, lists, links and form fields.</summary>
[NotInParallel]
public sealed class SemanticNodeTests
{
    /// <summary>The figure's place in the document node.</summary>
    private const int FigureIndex = 3;

    /// <summary>The second paragraph's place in the document node.</summary>
    private const int SecondIndex = 2;

    /// <summary>The form field's place in the document node.</summary>
    private const int FieldIndex = 2;

    /// <summary>The span of the wide cell.</summary>
    private const int WideSpan = 2;

    /// <summary>The nodes follow the tags' logical order, not the drawing order, and leave artifacts out.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FollowsLogicalOrder()
    {
        using var document = TaggedTestDocument.Open(TaggedSamples.Basic());
        var page = PdfReadingStructure.Read(document, 0);
        var root = page.Nodes[0];

        await Assert.That(page.Origin).IsEqualTo(PdfNodeOrigin.Tagged);
        await Assert.That(root.Role).IsEqualTo(PdfSemanticRole.Group);
        await Assert.That(root.Children[0].Role).IsEqualTo(PdfSemanticRole.Heading);
        await Assert.That(root.Children[0].Level).IsEqualTo(1);
        await Assert.That(root.Children[0].Text).IsEqualTo(TaggedSamples.Title);
        await Assert.That(root.Children[1].Text).IsEqualTo(TaggedSamples.First);
        await Assert.That(root.Children[SecondIndex].Text).IsEqualTo(TaggedSamples.Second);
        await Assert.That(root.Text).DoesNotContain(TaggedSamples.Header);
        await Assert.That(root.Children[1].Language).IsEqualTo(TaggedSamples.FrenchLanguage);
    }

    /// <summary>A figure is spoken by its description and has a focus box on its page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FiguresSpeakTheirDescription()
    {
        using var document = TaggedTestDocument.Open(TaggedSamples.Basic());
        var figure = PdfReadingStructure.Read(document, 0).Nodes[0].Children[FigureIndex];

        await Assert.That(figure.Role).IsEqualTo(PdfSemanticRole.Figure);
        await Assert.That(figure.SpokenText).IsEqualTo(TaggedSamples.FigureAlt);
        await Assert.That(figure.PageIndex).IsEqualTo(0);
        await Assert.That(figure.Bounds.IsEmpty).IsFalse();
    }

    /// <summary>A node's items map back to the page's glyphs and their text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ItemsMapBackToGlyphs()
    {
        using var document = TaggedTestDocument.Open(TaggedSamples.Basic());
        var page = PdfReadingStructure.Read(document, 0);
        var items = new List<int>();
        page.Nodes[0].Children[0].CollectItems(items);
        var text = new StringBuilder();
        foreach (var item in items)
        {
            _ = text.Append(page.Content!.GetText(item));
        }

        await Assert.That(text.ToString()).IsEqualTo(TaggedSamples.Title);
    }

    /// <summary>Cells are placed on the grid with their spans and linked to headers, explicit or inferred.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinksTableCellsToHeaders()
    {
        using var document = TaggedTestDocument.Open(TaggedSamples.TableAndList());
        var table = PdfReadingStructure.Read(document, 0).Nodes[0];

        await Assert.That(table.Role).IsEqualTo(PdfSemanticRole.Table);
        await Assert.That(HeaderTexts(Find(table, "30"))).IsEquivalentTo(["Age", "Ann"]);
        await Assert.That(HeaderTexts(Find(table, "Bob"))).IsEquivalentTo(["Name"]);
        await Assert.That(HeaderTexts(Find(table, "40"))).IsEquivalentTo(["Age"]);
        await Assert.That(Find(table, "Name").Scope).IsEqualTo(PdfTableScope.Column);
        await Assert.That(Find(table, "Ann").Scope).IsEqualTo(PdfTableScope.Row);
        await Assert.That(Find(table, "Total").ColumnSpan).IsEqualTo(WideSpan);
        await Assert.That(Find(table, "70").Column).IsEqualTo(WideSpan);
        await Assert.That(Find(table, "70").Row).IsEqualTo(WideSpan + 1);
    }

    /// <summary>Lists keep their items, labels and bodies, with the list's numbering.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsLists()
    {
        using var document = TaggedTestDocument.Open(TaggedSamples.TableAndList());
        var list = PdfReadingStructure.Read(document, 0).Nodes[1];
        var item = list.Children[0];

        await Assert.That(list.Role).IsEqualTo(PdfSemanticRole.List);
        await Assert.That(list.Element!.Attributes.ListNumbering).IsEqualTo("Decimal");
        await Assert.That(item.Role).IsEqualTo(PdfSemanticRole.ListItem);
        await Assert.That(item.Children[0].Role).IsEqualTo(PdfSemanticRole.ListLabel);
        await Assert.That(item.Children[0].Text).IsEqualTo("1.");
        await Assert.That(item.Children[1].Role).IsEqualTo(PdfSemanticRole.ListBody);
        await Assert.That(item.Text).IsEqualTo("1. Milk");
    }

    /// <summary>A link carries its annotation and address; a form field its label and state from the form reader.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsLinksAndFormFields()
    {
        using var document = TaggedTestDocument.Open(TaggedSamples.LinksAndForms());
        var root = PdfReadingStructure.Read(document, 0).Nodes[0];
        var link = root.Children[0];
        var field = root.Children[FieldIndex];

        await Assert.That(link.Role).IsEqualTo(PdfSemanticRole.Link);
        await Assert.That(link.Text).IsEqualTo(TaggedSamples.LinkText);
        await Assert.That(link.LinkUri).IsEqualTo(TaggedSamples.LinkUri);
        await Assert.That(link.AnnotationId.IsValid).IsTrue();
        await Assert.That(field.Role).IsEqualTo(PdfSemanticRole.FormField);
        await Assert.That(field.FieldLabel).IsEqualTo(TaggedSamples.FieldLabel);
        await Assert.That(field.FormField!.Value).IsEqualTo(TaggedSamples.FieldValue);
        await Assert.That(field.SpokenText).IsEqualTo(TaggedSamples.FieldLabel);
        await Assert.That(field.Bounds.IsEmpty).IsFalse();
    }

    /// <summary>An annotation's /StructParent leads back to the element that refers to it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationsLeadBackToTheirElements()
    {
        using var document = TaggedTestDocument.Open(TaggedSamples.LinksAndForms());
        var tree = PdfDocumentTagged.GetStructureTree(document)!;
        var annotations = PdfDocumentPages.GetPage(document, 0).Dictionary.GetArray(Objects.KnownName.Annots)!;

        await Assert.That(tree.GetObjectParent(annotations.GetDictionary(0)!)!.Type).IsEqualTo(PdfStructureType.Link);
        await Assert.That(tree.GetObjectParent(annotations.GetDictionary(1)!)!.Type).IsEqualTo(PdfStructureType.Form);
    }

    /// <summary>Finds a table cell by its text.</summary>
    /// <param name="node">The node to search.</param>
    /// <param name="text">The text.</param>
    /// <returns>The cell.</returns>
    /// <exception cref="InvalidOperationException">No cell has the text.</exception>
    private static PdfSemanticNode Find(PdfSemanticNode node, string text) => TryFind(node, text) ?? throw new InvalidOperationException(text);

    /// <summary>Searches for a table cell by its text.</summary>
    /// <param name="node">The node to search.</param>
    /// <param name="text">The text.</param>
    /// <returns>The cell, or <see langword="null"/>.</returns>
    private static PdfSemanticNode? TryFind(PdfSemanticNode node, string text)
    {
        if (node.Role is PdfSemanticRole.TableCell or PdfSemanticRole.TableHeaderCell && node.Text == text)
        {
            return node;
        }

        foreach (var child in node.Children)
        {
            if (TryFind(child, text) is { } found)
            {
                return found;
            }
        }

        return null;
    }

    /// <summary>Gets the texts of a cell's headers.</summary>
    /// <param name="cell">The cell.</param>
    /// <returns>The texts.</returns>
    private static string[] HeaderTexts(PdfSemanticNode cell)
    {
        var texts = new string[cell.Headers.Count];
        for (var i = 0; i < texts.Length; i++)
        {
            texts[i] = cell.Headers[i].Text;
        }

        return texts;
    }
}
