// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Tests.Conformance.VeraPdf;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>Checks standard type namespaces independently of PDF/UA identification.</summary>
public sealed class TagNamespaceWritingTests
{
    /// <summary>The two standard namespaces used by a mixed tree.</summary>
    private const int MixedNamespaces = 2;

    /// <summary>The structure root's namespace array key.</summary>
    private const string NamespacesKey = "Namespaces";

    /// <summary>The PDF 2.0 footnote or endnote spelling.</summary>
    private const string FootnoteType = "FENote";

    /// <summary>The PDF/UA-2 clause requiring a PDF 2.0 Document root.</summary>
    private const string DocumentNamespaceClause = "8.2.5.2";

    /// <summary>The side length of the blank page independently validated.</summary>
    private const float PageSide = 300F;

    /// <summary>A PDF 1.7 tree cannot write PDF 2.0-only names without extension mappings.</summary>
    /// <param name="type">The PDF 2.0-only standard spelling.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("DocumentFragment")]
    [Arguments("Aside")]
    [Arguments("Title")]
    [Arguments(FootnoteType)]
    [Arguments("Em")]
    [Arguments("Strong")]
    [Arguments("Sub")]
    [Arguments("Artifact")]
    public async Task Pdf17TreeRejectsPdf20OnlyType(string type)
    {
        var tags = new PdfTagTree(new PdfDocumentBuilder());
        await Assert.That(() => tags.AddElement(tags.Root, type)).Throws<ArgumentException>();
    }

    /// <summary>Legacy-only standard names in a PDF 2.0 tree retain their PDF 1.7 namespace.</summary>
    /// <param name="type">The standard spelling absent from the PDF 2.0 namespace.</param>
    /// <param name="cancellationToken">Cancels saving and reading.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("Art")]
    [Arguments("Private")]
    [Arguments("BlockQuote")]
    [Arguments("TOC")]
    [Arguments("TOCI")]
    [Arguments("Index")]
    [Arguments("Quote")]
    [Arguments("Note")]
    [Arguments("Reference")]
    [Arguments("BibEntry")]
    [Arguments("Code")]
    public async Task Pdf20TreeWritesLegacyTypeInPdf17Namespace(string type, CancellationToken cancellationToken)
    {
        var builder = new PdfDocumentBuilder { Version = PdfHeaderVersion.Pdf20 };
        var tags = new PdfTagTree(builder, true);
        _ = tags.AddElement(tags.Root, type);
        builder.CatalogEntries.Set(KnownName.StructTreeRoot, PdfValue.FromReference(tags.Finish()));
        await using var output = new MemoryStream();
        await builder.SaveAsync(output, cancellationToken);
        using var document = await PdfDocumentReader.OpenAsync(output.ToArray(), null, cancellationToken);
        var tree = PdfDocumentTagged.GetStructureTree(document)!;
        var element = tree.Roots[0].Kids[0].Element!;
        var rootDictionary = document.Objects.Catalog!.GetDictionary(KnownName.StructTreeRoot)!;

        await Assert.That(tree.Roots[0].Namespace).IsEqualTo(PdfStructureTypes.Pdf20Namespace);
        await Assert.That(element.Namespace).IsEqualTo(PdfStructureTypes.Pdf17Namespace);
        await Assert.That(element.RawType).IsEqualTo(type);
        await Assert.That(element.MappedType).IsEqualTo(type);
        await Assert.That(element.Type).IsNotEqualTo(PdfStructureType.Unknown);
        await Assert.That(rootDictionary.GetArray(document.Objects.Names.Intern(NamespacesKey))!.Count).IsEqualTo(MixedNamespaces);
    }

    /// <summary>An unclaimed PDF 2.0 creator still identifies its structure namespace.</summary>
    /// <param name="type">The PDF 2.0-only standard spelling.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("DocumentFragment")]
    [Arguments("Aside")]
    [Arguments("Title")]
    [Arguments(FootnoteType)]
    [Arguments("Em")]
    [Arguments("Strong")]
    [Arguments("Sub")]
    [Arguments("Artifact")]
    public async Task UnclaimedPdf20CreatorWritesPdf20Namespace(string type, CancellationToken cancellationToken)
    {
        var creator = new PdfDocumentCreator(new() { Tagged = true });
        _ = creator.Tags!.AddElement(creator.Tags.Root, type);
        using var document = await PdfDocumentReader.OpenAsync(creator.ToArray(), null, cancellationToken);
        var root = PdfDocumentTagged.GetStructureTree(document)!.Roots[0];

        await Assert.That(root.Namespace).IsEqualTo(PdfStructureTypes.Pdf20Namespace);
        await Assert.That(root.Kids[0].Element!.Namespace).IsEqualTo(PdfStructureTypes.Pdf20Namespace);
        await Assert.That(root.Kids[0].Element!.RawType).IsEqualTo(type);
        await Assert.That(root.Kids[0].Element!.Type).IsNotEqualTo(PdfStructureType.Unknown);
        await Assert.That(creator.Finish().ClaimsPdfUa).IsFalse();
    }

    /// <summary>A standalone legacy tree needs no namespace objects merely because its file version is PDF 2.0.</summary>
    /// <param name="type">The spelling usable in the default PDF 1.7 namespace.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("P")]
    [Arguments("Note")]
    public async Task UnclaimedLegacyTreeKeepsDefaultNamespace(string type, CancellationToken cancellationToken)
    {
        var creator = new PdfDocumentCreator(new() { Tagged = true });
        _ = creator.Tags!.AddElement(creator.Tags.Root, type);
        using var document = await PdfDocumentReader.OpenAsync(creator.ToArray(), null, cancellationToken);
        var root = PdfDocumentTagged.GetStructureTree(document)!.Roots[0];

        await Assert.That(root.Namespace).IsNull();
        await Assert.That(root.Kids[0].Element!.Namespace).IsNull();
        await Assert.That(document.Objects.Catalog.GetDictionary(KnownName.StructTreeRoot)!
            .Get(document.Objects.Names.Intern(NamespacesKey)).Kind).IsEqualTo(PdfKind.Null);
    }

    /// <summary>A new PDF 2.0 type promotes earlier common and legacy elements into their correct namespaces.</summary>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task NewTypePromotesExistingElements(CancellationToken cancellationToken)
    {
        var creator = new PdfDocumentCreator(new() { Tagged = true });
        var tags = creator.Tags!;
        _ = tags.AddElement(tags.Root, "Note");
        _ = tags.AddElement(tags.Root, "P");
        _ = tags.AddElement(tags.Root, FootnoteType);
        using var document = await PdfDocumentReader.OpenAsync(creator.ToArray(), null, cancellationToken);
        var kids = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids;

        await Assert.That(kids[0].Element!.Namespace).IsEqualTo(PdfStructureTypes.Pdf17Namespace);
        await Assert.That(kids[1].Element!.Namespace).IsEqualTo(PdfStructureTypes.Pdf20Namespace);
        await Assert.That(kids[^1].Element!.Namespace).IsEqualTo(PdfStructureTypes.Pdf20Namespace);
    }

    /// <summary>Common names use PDF 2.0 while legacy names share one distinct namespace object.</summary>
    /// <param name="cancellationToken">Cancels saving and reading.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task MixedTreeUsesEachNamespaceOnce(CancellationToken cancellationToken)
    {
        var builder = new PdfDocumentBuilder { Version = PdfHeaderVersion.Pdf20 };
        var tags = new PdfTagTree(builder, true);
        _ = tags.AddElement(tags.Root, "P");
        _ = tags.AddElement(tags.Root, "Note");
        _ = tags.AddElement(tags.Root, "Code");
        builder.CatalogEntries.Set(KnownName.StructTreeRoot, PdfValue.FromReference(tags.Finish()));
        await using var output = new MemoryStream();
        await builder.SaveAsync(output, cancellationToken);
        using var document = await PdfDocumentReader.OpenAsync(output.ToArray(), null, cancellationToken);
        var root = PdfDocumentTagged.GetStructureTree(document)!.Roots[0];
        var kids = root.Kids;

        await Assert.That(kids[0].Element!.Namespace).IsEqualTo(PdfStructureTypes.Pdf20Namespace);
        await Assert.That(kids[1].Element!.Namespace).IsEqualTo(PdfStructureTypes.Pdf17Namespace);
        await Assert.That(kids[1].Element!.Dictionary.GetRaw(document.Objects.Names.Intern("NS")))
            .IsEqualTo(kids[^1].Element!.Dictionary.GetRaw(document.Objects.Names.Intern("NS")));
        await Assert.That(document.Objects.Catalog!.GetDictionary(KnownName.StructTreeRoot)!
            .GetArray(document.Objects.Names.Intern(NamespacesKey))!.Count).IsEqualTo(MixedNamespaces);
        await Assert.That(tags.Finish()).IsEqualTo(tags.RootId);
    }

    /// <summary>Editing and either save format preserve the namespaces of a promoted mixed tree.</summary>
    /// <param name="incremental">Whether to append a revision instead of compacting.</param>
    /// <param name="cancellationToken">Cancels saving and reading.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EditedMixedTreePreservesNamespaces(bool incremental, CancellationToken cancellationToken)
    {
        var creator = new PdfDocumentCreator(new() { Tagged = true });
        var tags = creator.Tags!;
        _ = tags.AddElement(tags.Root, "Note");
        _ = tags.AddElement(tags.Root, FootnoteType);
        using var document = await PdfDocumentReader.OpenAsync(creator.ToArray(), null, cancellationToken);
        var note = PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;
        var edited = note.Dictionary.Clone();
        edited.Set(KnownName.T, PdfValue.FromString("edited note"u8.ToArray()));
        StoreEditing.Replace(document.Objects, note.Id, PdfValue.FromDictionary(edited));
        await using var output = new MemoryStream();
        if (incremental)
        {
            await PdfIncrementalWriter.SaveAsync(document.Objects, output, new(), cancellationToken);
        }
        else
        {
            await PdfCompactWriter.SaveAsync(document.Objects, PdfCompactOptions.Default, output, cancellationToken);
        }

        using var saved = await PdfDocumentReader.OpenAsync(output.ToArray(), null, cancellationToken);
        var root = PdfDocumentTagged.GetStructureTree(saved)!.Roots[0];
        await Assert.That(root.Namespace).IsEqualTo(PdfStructureTypes.Pdf20Namespace);
        await Assert.That(root.Kids[0].Element!.Namespace).IsEqualTo(PdfStructureTypes.Pdf17Namespace);
        await Assert.That(root.Kids[0].Element!.Title).IsEqualTo("edited note");
        await Assert.That(root.Kids[^1].Element!.Namespace).IsEqualTo(PdfStructureTypes.Pdf20Namespace);
        await Assert.That(saved.Objects.Catalog.GetDictionary(KnownName.StructTreeRoot)!
            .GetArray(saved.Objects.Names.Intern(NamespacesKey))!.Count).IsEqualTo(MixedNamespaces);
    }

    /// <summary>Checks that veraPDF accepts the promoted Document namespace and rejects a control without it.</summary>
    /// <param name="cancellationToken">Cancels writing and independent validation.</param>
    /// <returns>A task.</returns>
    [Test]
    [NotInParallel(nameof(VeraPdfValidator))]
    public async Task PromotedDocumentNamespaceAgreesWithVeraPdf(CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        using var folder = new PdfAOutputFolder("tag-namespace");
        var source = Path.Combine(folder.Path, "promoted.pdf");
        var control = Path.Combine(folder.Path, "missing-root-namespace.pdf");
        var creator = new PdfDocumentCreator(new() { Tagged = true });
        var tags = creator.Tags!;
        _ = tags.AddElement(tags.Root, "Code");
        _ = tags.AddElement(tags.Root, FootnoteType);
        {
            var page = creator.BeginPage(PageSide, PageSide);
            try
            {
                _ = page.Finish();
            }
            finally
            {
                page.Dispose();
            }
        }

        var bytes = creator.ToArray();
        await File.WriteAllBytesAsync(source, bytes, cancellationToken);
        using (var document = await PdfDocumentReader.OpenAsync(bytes, null, cancellationToken))
        {
            var root = PdfDocumentTagged.GetStructureTree(document)!.Roots[0];
            var edited = root.Dictionary.Clone();
            _ = edited.Remove(document.Objects.Names.Intern("NS"));
            StoreEditing.Replace(document.Objects, root.Id, PdfValue.FromDictionary(edited));
            await using var output = File.Create(control);

            // Keep the negative control invalid so the independent rule must demonstrate that it detects the defect.
            await PdfCompactWriter.SaveAsync(document.Objects, PdfCompactOptions.Default, output, cancellationToken);
        }

        var results = await VeraPdfValidator.ValidateAsync(home, "ua2", [source, control], cancellationToken);
        var promoted = results[Path.GetFullPath(source)];
        var missing = results[Path.GetFullPath(control)];
        await Assert.That(promoted.ProfileName).IsEqualTo("PDF/UA-2 + Tagged PDF validation profile");
        await Assert.That(missing.ProfileName).IsEqualTo(promoted.ProfileName);
        await Assert.That(promoted.Failures.Exists(static failure => failure.Clause == DocumentNamespaceClause))
            .IsFalse().Because(promoted.FailureSummary);
        await Assert.That(missing.Failures.Exists(static failure => failure.Clause == DocumentNamespaceClause))
            .IsTrue().Because("the control must fail the same independently checked namespace clause");
    }
}
