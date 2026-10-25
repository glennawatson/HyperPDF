// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Tagged;

/// <summary>
/// Edits annotations, form fields and the file layout through the library's writers, saves, reopens, and checks that
/// the page's /StructParents, the parent tree and the object references still resolve.
/// </summary>
[NotInParallel]
public sealed class StructurePreservationTests
{
    /// <summary>A red colour.</summary>
    private const uint Red = 0xFF0000;

    /// <summary>The added annotation's left edge.</summary>
    private const float NoteLeft = 400;

    /// <summary>The added annotation's bottom edge.</summary>
    private const float NoteBottom = 400;

    /// <summary>The added annotation's right edge.</summary>
    private const float NoteRight = 450;

    /// <summary>The added annotation's top edge.</summary>
    private const float NoteTop = 450;

    /// <summary>The annotations after one is added.</summary>
    private const int AnnotationsAfterAdd = 3;

    /// <summary>The text typed into the field.</summary>
    private const string TypedValue = "Bob";

    /// <summary>The form field node's place in the document node.</summary>
    private const int FieldNode = 2;

    /// <summary>Editing the link annotation and adding another keeps the link's /StructParent and its object reference.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationEditsKeepStructure()
    {
        var saved = Edit(static (document, page) =>
        {
            var store = document.Objects;
            var link = PdfPageAnnotations.Get(store, page, 0)!.Clone();
            PdfAnnotations.SetColor(link, KnownName.C, Red);
            _ = PdfPageAnnotations.Replace(store, page, 0, link);
            _ = PdfPageAnnotations.Append(store, page, PdfAnnotations.Create(store, KnownName.Square, new(NoteLeft, NoteBottom, NoteRight, NoteTop)));
        });

        using var reopened = TaggedTestDocument.Open(saved);

        await Assert.That(Annotations(reopened).Count).IsEqualTo(AnnotationsAfterAdd);
        await AssertResolvesAsync(reopened, 0, TaggedSamples.FieldIndex);
        await Assert.That(ReadNodes(reopened)[0].LinkUri).IsEqualTo(TaggedSamples.LinkUri);
    }

    /// <summary>Filling in the field keeps its /StructParent, and the node reads the new value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FormEditsKeepStructure()
    {
        var saved = Edit(static (document, page) => _ = HyperPdfLibrary.Forms.PdfFormEditing.SetText(PdfDocumentForms.GetForm(document), page.Index, TaggedSamples.FieldIndex, TypedValue));

        using var reopened = TaggedTestDocument.Open(saved);
        var field = ReadNodes(reopened)[FieldNode];

        await AssertResolvesAsync(reopened, 0, TaggedSamples.FieldIndex);
        await Assert.That(field.FormField!.Value).IsEqualTo(TypedValue);
        await Assert.That(field.FieldLabel).IsEqualTo(TaggedSamples.FieldLabel);
    }

    /// <summary>Removing the link moves the field down a place; the field still resolves by its object, not its index.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovingAnAnnotationKeepsTheOthers()
    {
        var saved = Edit(static (document, page) => _ = PdfPageAnnotations.RemoveAt(document.Objects, page, 0));

        using var reopened = TaggedTestDocument.Open(saved);
        var tree = PdfDocumentTagged.GetStructureTree(reopened)!;
        var widget = Annotations(reopened).GetDictionary(0)!;
        var nodes = ReadNodes(reopened);

        await Assert.That(tree.GetObjectParent(widget)!.Type).IsEqualTo(PdfStructureType.Form);
        await Assert.That(nodes[0].Annotation).IsNull();
        await Assert.That(nodes[0].Text).IsEqualTo(TaggedSamples.LinkText);
        await Assert.That(nodes[FieldNode].FormField!.Value).IsEqualTo(TaggedSamples.FieldValue);
    }

    /// <summary>Rewriting the whole file, with object streams, renumbers nothing the structure needs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CompactRewriteKeepsStructure()
    {
        byte[] saved;
        using (var document = TaggedTestDocument.Open(TaggedSamples.LinksAndForms()))
        {
            saved = PdfCompactWriter.Save(document.Objects, PdfCompactOptions.Default);
        }

        using var reopened = TaggedTestDocument.Open(saved);

        await AssertResolvesAsync(reopened, 0, TaggedSamples.FieldIndex);
    }

    /// <summary>Opens the links and forms sample, applies an edit, and saves it incrementally.</summary>
    /// <param name="edit">The edit.</param>
    /// <returns>The saved file.</returns>
    private static byte[] Edit(Action<PdfDocument, PdfPage> edit)
    {
        using var document = TaggedTestDocument.Open(TaggedSamples.LinksAndForms());
        edit(document, PdfDocumentPages.GetPage(document, 0));
        return PdfIncrementalWriter.Save(document.Objects);
    }

    /// <summary>Gets the page's annotations.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The <c>/Annots</c> array.</returns>
    private static PdfArray Annotations(PdfDocument document) => PdfDocumentPages.GetPage(document, 0).Dictionary.GetArray(KnownName.Annots)!;

    /// <summary>Reads the document node's children.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The nodes.</returns>
    private static IReadOnlyList<PdfSemanticNode> ReadNodes(PdfDocument document) => PdfReadingStructure.ReadTagged(document, 0).Nodes[0].Children;

    /// <summary>Checks the page's parent tree entry, both annotations' /StructParent and both object references.</summary>
    /// <param name="document">The reopened document.</param>
    /// <param name="linkIndex">The link's index in /Annots.</param>
    /// <param name="fieldIndex">The field's index in /Annots.</param>
    /// <returns>A task.</returns>
    private static async Task AssertResolvesAsync(PdfDocument document, int linkIndex, int fieldIndex)
    {
        var tree = PdfDocumentTagged.GetStructureTree(document)!;
        var annotations = Annotations(document);
        var linkElement = tree.GetObjectParent(annotations.GetDictionary(linkIndex)!);
        var formElement = tree.GetObjectParent(annotations.GetDictionary(fieldIndex)!);

        await Assert.That(tree.GetStructParents(0)).IsEqualTo(0);
        await Assert.That(tree.GetMarkedContentParent(0, 0)).IsSameReferenceAs(linkElement);
        await Assert.That(linkElement!.Type).IsEqualTo(PdfStructureType.Link);
        await Assert.That(formElement!.Type).IsEqualTo(PdfStructureType.Form);
        await Assert.That(ObjectOf(linkElement).Number).IsEqualTo(annotations.GetRaw(linkIndex).AsReference().Number);
        await Assert.That(ObjectOf(formElement).Number).IsEqualTo(annotations.GetRaw(fieldIndex).AsReference().Number);
    }

    /// <summary>Gets the object an element's object reference kid names.</summary>
    /// <param name="element">The element.</param>
    /// <returns>The object id, or an invalid id.</returns>
    private static PdfObjectId ObjectOf(PdfStructureElement element)
    {
        foreach (var kid in element.Kids)
        {
            if (kid.Kind == PdfStructureKidKind.Object)
            {
                return kid.Object;
            }
        }

        return default;
    }
}
