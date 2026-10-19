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

/// <summary>Checks optional list-numbering names and their strict creation rules.</summary>
public sealed class ListNumberingWritingTests
{
    /// <summary>The number of separately owned attribute objects.</summary>
    private const int ThreeOwners = 3;

    /// <summary>The side length of a blank page.</summary>
    private const float PageSide = 300F;

    /// <summary>An undefined numbering scheme.</summary>
    private const int UndefinedNumbering = 99;

    /// <summary>The description used to keep a replacement options record distinct.</summary>
    private const string Description = "a list";

    /// <summary>The independently checked PDF/UA-2 and WTPDF list clause.</summary>
    private const string ListClause = "8.2.5.25";

    /// <summary>The rule requiring non-None numbering for labelled lists.</summary>
    private const string ListLabelRule = "1";

    /// <summary>Gets the list-numbering attribute key.</summary>
    private static ReadOnlySpan<byte> ListNumberingKey => "ListNumbering"u8;

    /// <summary>Every standard numbering value is a List-owned name and survives parsing.</summary>
    /// <param name="numbering">The requested scheme.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfListNumbering.None)]
    [Arguments(PdfListNumbering.Disc)]
    [Arguments(PdfListNumbering.Circle)]
    [Arguments(PdfListNumbering.Square)]
    [Arguments(PdfListNumbering.Decimal)]
    [Arguments(PdfListNumbering.UpperRoman)]
    [Arguments(PdfListNumbering.LowerRoman)]
    [Arguments(PdfListNumbering.UpperAlpha)]
    [Arguments(PdfListNumbering.LowerAlpha)]
    [Arguments(PdfListNumbering.Unordered)]
    [Arguments(PdfListNumbering.Ordered)]
    [Arguments(PdfListNumbering.Description)]
    public async Task StandardNamesRoundTrip(PdfListNumbering numbering, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(CreateBytes(new() { ListNumbering = numbering }), null, cancellationToken);
        var list = ReadList(document);
        var attributes = list.Dictionary.GetDictionary(KnownName.A);
        await Assert.That(attributes).IsNotNull();
        var names = document.Objects.Names;
        await Assert.That(names.GetString(attributes!.Get(KnownName.O).AsName())).IsEqualTo("List");
        var value = attributes.Get(names.Intern(ListNumberingKey));
        await Assert.That(value.Kind).IsEqualTo(PdfKind.Name);
        await Assert.That(names.GetString(value.AsName())).IsEqualTo(numbering.ToString());
        await Assert.That(list.Attributes.ListNumbering).IsEqualTo(numbering.ToString());
    }

    /// <summary>Omission writes no attribute dictionary; explicit None remains a real attribute.</summary>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task OmittedNumberingHasNoAttributes(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(CreateBytes(PdfTagOptions.Default), null, cancellationToken);
        await Assert.That(ReadList(document).Dictionary.Get(KnownName.A).IsNull).IsTrue();
        await Assert.That(PdfTagOptions.Default.ListNumbering).IsNull();
    }

    /// <summary>Undefined schemes cannot silently become omission or private names.</summary>
    /// <param name="value">The invalid value.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(-1)]
    [Arguments(UndefinedNumbering)]
    public async Task UndefinedNumberingIsRejected(int value)
    {
        var tags = new PdfTagTree(new PdfDocumentBuilder(), true);
        await Assert.That(() => tags.AddElement(tags.Root, "L", new() { ListNumbering = (PdfListNumbering)value }))
            .Throws<ArgumentOutOfRangeException>();
        await Assert.That(tags.Root.Kids).IsEmpty();
    }

    /// <summary>List attributes cannot be written on unrelated structure elements.</summary>
    /// <param name="type">The unrelated structure type.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("P")]
    [Arguments("LI")]
    [Arguments("LBody")]
    [Arguments("TH")]
    [Arguments("FENote")]
    public async Task NumberingOnNonListsIsRejected(string type)
    {
        var tags = new PdfTagTree(new PdfDocumentBuilder(), true);
        await Assert.That(() => tags.AddElement(tags.Root, type, new() { ListNumbering = PdfListNumbering.Decimal }))
            .Throws<ArgumentException>();
        await Assert.That(tags.Root.Kids).IsEmpty();
    }

    /// <summary>PDF 2.0-only schemes are not written through a PDF 1.7 tree.</summary>
    /// <param name="numbering">The PDF 2.0 scheme.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfListNumbering.Unordered)]
    [Arguments(PdfListNumbering.Ordered)]
    [Arguments(PdfListNumbering.Description)]
    public async Task Pdf20NamesRequirePdf20Output(PdfListNumbering numbering)
    {
        var tags = new PdfTagTree(new PdfDocumentBuilder { Version = PdfHeaderVersion.Pdf17 });
        await Assert.That(() => tags.AddElement(tags.Root, "L", new() { ListNumbering = numbering }))
            .Throws<ArgumentException>();
        await Assert.That(tags.Root.Kids).IsEmpty();
    }

    /// <summary>A PDF 1.7 tree accepts its standard list-numbering values.</summary>
    /// <param name="numbering">The PDF 1.7 scheme.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfListNumbering.None)]
    [Arguments(PdfListNumbering.Disc)]
    [Arguments(PdfListNumbering.Circle)]
    [Arguments(PdfListNumbering.Square)]
    [Arguments(PdfListNumbering.Decimal)]
    [Arguments(PdfListNumbering.UpperRoman)]
    [Arguments(PdfListNumbering.LowerRoman)]
    [Arguments(PdfListNumbering.UpperAlpha)]
    [Arguments(PdfListNumbering.LowerAlpha)]
    public async Task Pdf17SchemesRemainAvailable(PdfListNumbering numbering)
    {
        var builder = new PdfDocumentBuilder { Version = PdfHeaderVersion.Pdf17 };
        var tags = new PdfTagTree(builder);
        _ = tags.AddElement(tags.Root, "L", new() { ListNumbering = numbering });
        using var document = PdfDocumentReader.Open(FinishBuilder(builder, tags), null);
        var attributes = ReadList(document).Dictionary.GetDictionary(KnownName.A);
        await Assert.That(attributes).IsNotNull();
        await Assert.That(document.Objects.Names.GetString(attributes!.Get(document.Objects.Names.Intern(ListNumberingKey)).AsName()))
            .IsEqualTo(numbering.ToString());
    }

    /// <summary>A PDF 2.0 file permits its list attributes on a default PDF 1.7 structure element.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Pdf20AttributesDoNotRequirePdf20ElementNamespace()
    {
        var builder = new PdfDocumentBuilder { Version = PdfHeaderVersion.Pdf20 };
        var tags = new PdfTagTree(builder);
        _ = tags.AddElement(tags.Root, "L", new() { ListNumbering = PdfListNumbering.Ordered });
        using var document = PdfDocumentReader.Open(FinishBuilder(builder, tags), null);
        var list = ReadList(document);
        await Assert.That(list.Attributes.ListNumbering).IsEqualTo("Ordered");
        await Assert.That(list.Dictionary.Get(document.Objects.Names.Intern("NS"u8)).IsNull).IsTrue();
    }

    /// <summary>A namespace flag cannot authorize PDF 2.0 attributes in an older file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NamespaceFlagDoesNotOverrideOutputVersion()
    {
        var builder = new PdfDocumentBuilder { Version = PdfHeaderVersion.Pdf17 };
        var tags = new PdfTagTree(builder, true);
        await Assert.That(() => tags.AddElement(tags.Root, "L", new() { ListNumbering = PdfListNumbering.Ordered }))
            .Throws<ArgumentException>();
        await Assert.That(tags.Root.Kids).IsEmpty();
    }

    /// <summary>Changing the output version cannot silently serialize incompatible list attributes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DowngradedOutputIsRejectedBeforeSerializingAttributes()
    {
        var builder = new PdfDocumentBuilder();
        var tags = new PdfTagTree(builder);
        _ = tags.AddElement(tags.Root, "L", new() { ListNumbering = PdfListNumbering.Ordered });
        builder.Version = PdfHeaderVersion.Pdf17;
        await Assert.That(() => tags.Finish()).Throws<ArgumentException>();
    }

    /// <summary>A catalog version upgrade permits PDF 2.0 attributes on an edited PDF 1.7 file.</summary>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task CatalogVersionUpgradePermitsPdf20Attributes(CancellationToken cancellationToken)
    {
        var builder = new PdfDocumentBuilder { Version = PdfHeaderVersion.Pdf17 };
        var original = new PdfTagTree(builder);
        using var document = await PdfDocumentReader.OpenAsync(FinishBuilder(builder, original), null, cancellationToken);
        var store = document.Objects;
        store.Catalog.Set(store.Names.Intern("Version"u8), PdfValue.FromName(store.Names.Intern("2.0"u8)));
        var tags = new PdfTagTree(new PdfObjectStoreSink(store));
        _ = tags.AddElement(tags.Root, "L", new() { ListNumbering = PdfListNumbering.Ordered });
        await Assert.That(tags.Finish().IsValid).IsTrue();
    }

    /// <summary>Editing a list preserves its numbering through compact and incremental saves.</summary>
    /// <param name="numbering">The explicit scheme, including None.</param>
    /// <param name="incremental">Whether to append a revision.</param>
    /// <param name="cancellationToken">Cancels reading and saving.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfListNumbering.None, false)]
    [Arguments(PdfListNumbering.None, true)]
    [Arguments(PdfListNumbering.Decimal, false)]
    [Arguments(PdfListNumbering.Decimal, true)]
    [Arguments(PdfListNumbering.Ordered, false)]
    [Arguments(PdfListNumbering.Ordered, true)]
    [Arguments(PdfListNumbering.Description, false)]
    [Arguments(PdfListNumbering.Description, true)]
    public async Task EditedNumberingRoundTrips(PdfListNumbering numbering, bool incremental, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(CreateBytes(new() { ListNumbering = numbering }), null, cancellationToken);
        var list = ReadList(document);
        var edited = list.Dictionary.Clone();
        edited.Set(KnownName.T, PdfValue.FromString("edited list"u8.ToArray()));
        StoreEditing.Replace(document.Objects, list.Id, PdfValue.FromDictionary(edited));
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
        var savedList = ReadList(saved);
        await Assert.That(savedList.Title).IsEqualTo("edited list");
        await Assert.That(savedList.Attributes.ListNumbering).IsEqualTo(numbering.ToString());
        await Assert.That(savedList.Dictionary.GetDictionary(KnownName.A)!.Get(saved.Objects.Names.Intern(ListNumberingKey)).Kind)
            .IsEqualTo(PdfKind.Name);
    }

    /// <summary>Separate owners retain their own dictionaries when a list also has other attributes.</summary>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task MultipleOwnersRemainSeparate(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(
            CreateBytes(new() { ListNumbering = PdfListNumbering.Decimal, NoteType = PdfNoteType.Footnote, TableScope = PdfTableScope.Row }),
            null,
            cancellationToken);
        var attributes = ReadList(document).Dictionary.GetArray(KnownName.A)!;
        await Assert.That(attributes).IsNotNull();
        await Assert.That(attributes.Count).IsEqualTo(ThreeOwners);
        var owners = new HashSet<string>(StringComparer.Ordinal);
        for (var index = 0; index < attributes.Count; index++)
        {
            var dictionary = attributes.Get(index).AsDictionary()!;
            var owner = document.Objects.Names.GetString(dictionary.Get(KnownName.O).AsName());
            await Assert.That(owners.Add(owner)).IsTrue();
            await Assert.That(dictionary.Get(document.Objects.Names.Intern(ListNumberingKey)).IsNull)
                .IsEqualTo(owner != "List");
        }

        await Assert.That(owners).Contains("Table");
        await Assert.That(owners).Contains("FENote");
        await Assert.That(owners).Contains("List");
    }

    /// <summary>Record copies preserve both optional attributes without modifying their source.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecordCopiesKeepIndependentAttributes()
    {
        var original = new PdfTagOptions { NoteType = PdfNoteType.Endnote, ListNumbering = PdfListNumbering.Ordered };
        var changed = original with { ListNumbering = PdfListNumbering.Description, AlternativeText = Description };
        var omitted = changed with { NoteType = null, ListNumbering = null };
        var copied = new PdfTagOptions(changed, PdfListNumbering.Decimal);
        await Assert.That(copied).IsEqualTo(changed with { ListNumbering = PdfListNumbering.Decimal });
        await Assert.That(original.NoteType).IsEqualTo(PdfNoteType.Endnote);
        await Assert.That(original.ListNumbering).IsEqualTo(PdfListNumbering.Ordered);
        await Assert.That(changed.NoteType).IsEqualTo(PdfNoteType.Endnote);
        await Assert.That(changed.ListNumbering).IsEqualTo(PdfListNumbering.Description);
        await Assert.That(omitted.NoteType).IsNull();
        await Assert.That(omitted.ListNumbering).IsNull();
        await Assert.That(omitted.AlternativeText).IsEqualTo(Description);
        await Assert.That(original).IsEqualTo(new PdfTagOptions { NoteType = PdfNoteType.Endnote, ListNumbering = PdfListNumbering.Ordered });
    }

    /// <summary>Independent validators recognize written list numbering and detect absent or None numbering on labelled lists.</summary>
    /// <param name="flavour">The independent PDF/UA-2 or WTPDF profile.</param>
    /// <param name="profileName">The expected profile title.</param>
    /// <param name="cancellationToken">Cancels writing and validation.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("ua2", "PDF/UA-2 + Tagged PDF validation profile")]
    [Arguments("wt1r", "WTPDF 1.0 Reuse validation profile")]
    [Arguments("wt1a", "WTPDF 1.0 Accessibility validation profile")]
    [NotInParallel(nameof(VeraPdfValidator))]
    public async Task GeneratedLabelsAgreeWithVeraPdf(string flavour, string profileName, CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        using var folder = new PdfAOutputFolder("list-numbering");
        PdfListNumbering?[] schemes =
        [
            null, PdfListNumbering.None, PdfListNumbering.Disc, PdfListNumbering.Circle,
            PdfListNumbering.Square, PdfListNumbering.Decimal, PdfListNumbering.UpperRoman,
            PdfListNumbering.LowerRoman, PdfListNumbering.UpperAlpha, PdfListNumbering.LowerAlpha,
            PdfListNumbering.Unordered, PdfListNumbering.Ordered, PdfListNumbering.Description,
        ];
        var files = new List<string>();
        foreach (var scheme in schemes)
        {
            var path = Path.Combine(folder.Path, $"{scheme?.ToString() ?? "omitted"}.pdf");
            await File.WriteAllBytesAsync(path, CreateBytes(new() { ListNumbering = scheme }, true), cancellationToken);
            files.Add(path);
        }

        var jobs = await VeraPdfValidator.ValidateAsync(home, flavour, files, cancellationToken);
        for (var index = 0; index < schemes.Length; index++)
        {
            var job = jobs[Path.GetFullPath(files[index])];
            await Assert.That(job.ProfileName).IsEqualTo(profileName);
            var labelsFailed = job.Failures.Exists(static failure =>
                failure.Clause == ListClause && failure.TestNumber == ListLabelRule);
            await Assert.That(labelsFailed).IsEqualTo(schemes[index] is null or PdfListNumbering.None).Because(job.FailureSummary);
        }
    }

    /// <summary>Builds a list on a blank page without a conformance claim.</summary>
    /// <param name="options">The list's attributes.</param>
    /// <returns>The complete PDF bytes.</returns>
    private static byte[] CreateBytes(PdfTagOptions options) => CreateBytes(options, false);

    /// <summary>Builds a list with optional labels without claiming conformance.</summary>
    /// <param name="options">The list's attributes.</param>
    /// <param name="labels">Whether the list contains a label element.</param>
    /// <returns>The complete PDF bytes.</returns>
    private static byte[] CreateBytes(PdfTagOptions options, bool labels)
    {
        var creator = new PdfDocumentCreator(new() { Tagged = true });
        var tags = creator.Tags!;
        var list = tags.AddElement(tags.Root, "L", options);
        var item = tags.AddElement(list, "LI");
        if (labels)
        {
            _ = tags.AddElement(item, "Lbl");
        }

        _ = tags.AddElement(item, "LBody");
        using (var page = creator.BeginPage(PageSide, PageSide))
        {
            _ = page.Finish();
        }

        return creator.ToArray();
    }

    /// <summary>Gets the generated list element.</summary>
    /// <param name="document">The open document.</param>
    /// <returns>The list element.</returns>
    private static PdfStructureElement ReadList(PdfDocument document) =>
        PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;

    /// <summary>Attaches a completed structure tree and a blank page to a low-level builder.</summary>
    /// <param name="builder">The builder with the required output version.</param>
    /// <param name="tags">The structure tree.</param>
    /// <returns>The complete file bytes.</returns>
    private static byte[] FinishBuilder(PdfDocumentBuilder builder, PdfTagTree tags)
    {
        builder.CatalogEntries.Set(builder.Names.Intern("StructTreeRoot"u8), PdfValue.FromReference(tags.Finish()));
        _ = builder.AddSheet(PageSide, PageSide, []);
        return builder.ToArray();
    }
}
