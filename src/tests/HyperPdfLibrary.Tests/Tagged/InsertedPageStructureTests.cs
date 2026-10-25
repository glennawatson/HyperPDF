// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Tagged;

/// <summary>Tests that inserted pages bring their structure elements into a tagged target.</summary>
public sealed class InsertedPageStructureTests
{
    /// <summary>The marked content id of the heading.</summary>
    private const int HeadingMcid = 0;

    /// <summary>The marked content id of the second paragraph.</summary>
    private const int SecondMcid = 2;

    /// <summary>The first parent tree key the target has free.</summary>
    private const int FirstFreeKey = 1;

    /// <summary>The number of top-level elements after one insert.</summary>
    private const int RootsAfterInsert = 2;

    /// <summary>The form field's place among the inserted page's annotations.</summary>
    private const int FieldIndex = 1;

    /// <summary>The element type only the source's role map knows.</summary>
    private const string CalloutRole = "Callout";

    /// <summary>The left edge of the callout text.</summary>
    private const int TextLeft = 72;

    /// <summary>The baseline of the callout text.</summary>
    private const int TextBaseline = 700;

    /// <summary>Inserting a tagged page into a tagged document copies its elements, with new parent tree keys and the same MCIDs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TaggedPageBringsItsElements()
    {
        using var source = PdfDocumentReader.Open(TaggedSamples.Basic(), null);
        using var document = PdfDocumentReader.Open(TaggedSamples.Basic(), null);

        PdfDocumentPageOperations.InsertPages(document, 1, source, [0]);

        await AssertInserted(document);
        using var reopened = PdfDocumentReader.Open(PdfIncrementalWriter.Save(document.Objects), null);
        await AssertInserted(reopened);
        using var compact = PdfDocumentReader.Open(PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default), null);
        await AssertInserted(compact);
    }

    /// <summary>Role map entries the target lacks are added, and the target's own entries stay.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RoleMapEntriesAreMerged()
    {
        using var source = PdfDocumentReader.Open(CalloutDocument(), null);
        using var document = PdfDocumentReader.Open(TaggedSamples.Basic(), null);

        PdfDocumentPageOperations.InsertPages(document, 0, source, [0]);

        var roleMap = PdfStructureTree.Load(document)!.Root.GetDictionary(document.Objects.Names.Intern("RoleMap"))!;
        await Assert.That(roleMap.ContainsKey(document.Objects.Names.Intern(CalloutRole))).IsTrue();
        await Assert.That(roleMap.ContainsKey(document.Objects.Names.Intern("Heading1"))).IsTrue();
        await Assert.That(PdfStructureTree.Load(document)!.GetMarkedContentParent(0, HeadingMcid)!.MappedType).IsEqualTo("P");
    }

    /// <summary>Annotations of an inserted page keep their link to the element that refers to them.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationsKeepTheirElements()
    {
        using var source = PdfDocumentReader.Open(TaggedSamples.LinksAndForms(), null);
        using var document = PdfDocumentReader.Open(TaggedSamples.Basic(), null);

        PdfDocumentPageOperations.InsertPages(document, 0, source, [0]);

        var tree = PdfStructureTree.Load(document)!;
        var annotations = PdfDocumentPages.GetPage(document, 0).Dictionary.GetArray(KnownName.Annots)!;
        await Assert.That(tree.GetObjectParent(annotations.GetDictionary(0)!)!.Type).IsEqualTo(PdfStructureType.Link);
        await Assert.That(tree.GetObjectParent(annotations.GetDictionary(FieldIndex)!)!.Type).IsEqualTo(PdfStructureType.Form);
        await Assert.That(tree.GetMarkedContentParent(0, HeadingMcid)!.Type).IsEqualTo(PdfStructureType.Link);
        await Assert.That(tree.GetMarkedContentParent(1, HeadingMcid)!.PageIndex).IsEqualTo(1);
    }

    /// <summary>An untagged target gets no structure, and an untagged source adds none to a tagged target.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UntaggedDocumentsKeepCurrentBehaviour()
    {
        using var tagged = PdfDocumentReader.Open(TaggedSamples.Basic(), null);
        using var plain = PdfDocumentReader.Open(TaggedSamples.TwoColumns(), null);
        using var untaggedTarget = PdfDocumentReader.Open(TaggedSamples.TwoColumns(), null);
        using var taggedTarget = PdfDocumentReader.Open(TaggedSamples.Basic(), null);

        PdfDocumentPageOperations.InsertPages(untaggedTarget, 1, tagged, [0]);
        PdfDocumentPageOperations.InsertPages(taggedTarget, 1, plain, [0]);

        await Assert.That(untaggedTarget.Catalog.ContainsKey(KnownName.StructTreeRoot)).IsFalse();
        await Assert.That(PdfDocumentPages.GetPage(untaggedTarget, 1).Dictionary.ContainsKey(KnownName.StructParents)).IsFalse();
        await Assert.That(PdfStructureTree.Load(taggedTarget)!.Roots.Count).IsEqualTo(1);
        await Assert.That(PdfDocumentPages.GetPage(taggedTarget, 1).Dictionary.ContainsKey(KnownName.StructParents)).IsFalse();
    }

    /// <summary>Builds a tagged page whose element type is mapped by a role map the other documents lack.</summary>
    /// <returns>The PDF.</returns>
    private static byte[] CalloutDocument()
    {
        var pdf = new TaggedPdfBuilder { Content = TaggedPdfBuilder.Text(CalloutRole, HeadingMcid, TextLeft, TextBaseline, "Hi") };
        var root = pdf.Reserve();
        var document = pdf.Reserve();
        var element = pdf.Element(CalloutRole, document, "/K 0");
        var parents = pdf.Add(Format($"<< /Nums [0 [{element} 0 R]] >>"));
        pdf.SetElement(document, "Document", root, Format($"/K [{element} 0 R]"));
        pdf.Set(root, Format($"<< /Type /StructTreeRoot /K {document} 0 R /ParentTree {parents} 0 R /RoleMap << /Callout /P >> >>"));
        pdf.Catalog = Format($"/MarkInfo << /Marked true >> /StructTreeRoot {root} 0 R");
        return pdf.Build();
    }

    /// <summary>Formats an interpolated PDF snippet with the invariant culture.</summary>
    /// <param name="text">The snippet.</param>
    /// <returns>The formatted snippet.</returns>
    private static string Format(FormattableString text) => text.ToString(CultureInfo.InvariantCulture);

    /// <summary>Checks the document that has the sample page inserted after its own.</summary>
    /// <param name="document">The document.</param>
    /// <returns>A task.</returns>
    private static async Task AssertInserted(PdfDocument document)
    {
        var tree = PdfStructureTree.Load(document)!;

        await Assert.That(document.PageCount).IsEqualTo(RootsAfterInsert);
        await Assert.That(tree.Roots.Count).IsEqualTo(RootsAfterInsert);
        await Assert.That(tree.GetStructParents(0)).IsEqualTo(0);
        await Assert.That(tree.GetStructParents(1)).IsEqualTo(FirstFreeKey);
        var original = tree.GetMarkedContentParent(0, HeadingMcid)!;
        var copied = tree.GetMarkedContentParent(1, HeadingMcid)!;
        await Assert.That(original.PageIndex).IsEqualTo(0);
        await Assert.That(copied.PageIndex).IsEqualTo(1);
        await Assert.That(copied.RawType).IsEqualTo("Heading1");
        await Assert.That(copied.Parent).IsSameReferenceAs(tree.Roots[1]);
        await Assert.That(original.Parent).IsSameReferenceAs(tree.Roots[0]);
        await Assert.That(tree.GetMarkedContentParent(1, SecondMcid)!.RawType).IsEqualTo("P");
    }
}
