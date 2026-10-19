// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Tests.Conformance.VeraPdf;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Creation;

/// <summary>Checks strict creation of the non-interactive field attributes in ISO 32000 Tables 348 and 383.</summary>
public sealed class PrintFieldWritingTests
{
    /// <summary>The side length of the blank page.</summary>
    private const float PageSide = 300F;

    /// <summary>The side length of the field's marked graphic.</summary>
    private const float FieldSide = 20F;

    /// <summary>The two independently owned attribute dictionaries.</summary>
    private const int TwoOwners = 2;

    /// <summary>The checkbox's alternate field name.</summary>
    private const string FieldDescription = "Accept";

    /// <summary>The PDF 2.0 spelling of the field state key.</summary>
    private const string CheckedKey = "Checked";

    /// <summary>The PDF 1.7 spelling of the field state key.</summary>
    private const string LegacyCheckedKey = "checked";

    /// <summary>Each standard field role survives creation in the version defining it.</summary>
    /// <param name="role">The standard role.</param>
    /// <param name="pdf20">Whether to write PDF 2.0 instead of PDF 1.7.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("rb", false)]
    [Arguments("cb", false)]
    [Arguments("pb", false)]
    [Arguments("tv", false)]
    [Arguments("rb", true)]
    [Arguments("cb", true)]
    [Arguments("pb", true)]
    [Arguments("tv", true)]
    [Arguments("lb", true)]
    public async Task StandardRoleRoundTrips(string role, bool pdf20, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(CreateBytes(new() { PrintField = new(role, null, null) }, pdf20), null, cancellationToken);
        var field = ReadField(document);
        var attributes = field.Dictionary.GetDictionary(KnownName.A)!;
        var names = document.Objects.Names;
        await Assert.That(names.GetString(attributes.Get(KnownName.O).AsName())).IsEqualTo("PrintField");
        await Assert.That(attributes.Get(names.Intern("Role"u8)).Kind).IsEqualTo(PdfKind.Name);
        await Assert.That(field.Attributes.PrintField.Role).IsEqualTo(role);
        await Assert.That(field.Attributes.PrintField.Checked).IsNull();
        await Assert.That(field.Attributes.PrintField.Description).IsNull();
    }

    /// <summary>Checkbox states use the version's spelling and never emit both spellings.</summary>
    /// <param name="state">The standard state name.</param>
    /// <param name="pdf20">Whether to write PDF 2.0.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("on", false)]
    [Arguments("off", false)]
    [Arguments("neutral", false)]
    [Arguments("on", true)]
    [Arguments("off", true)]
    [Arguments("neutral", true)]
    public async Task CheckedStateUsesOutputVersion(string state, bool pdf20, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(CreateBytes(new() { PrintField = new("cb", state, null) }, pdf20), null, cancellationToken);
        var field = ReadField(document);
        var attributes = field.Dictionary.GetDictionary(KnownName.A)!;
        var names = document.Objects.Names;
        var written = attributes.Get(names.Intern(pdf20 ? CheckedKey : LegacyCheckedKey));
        await Assert.That(written.Kind).IsEqualTo(PdfKind.Name);
        await Assert.That(names.GetString(written.AsName())).IsEqualTo(state);
        await Assert.That(attributes.Get(names.Intern(pdf20 ? LegacyCheckedKey : CheckedKey)).IsNull).IsTrue();
        await Assert.That(field.Attributes.PrintField.Checked).IsEqualTo(state);
    }

    /// <summary>Field descriptions preserve empty strings and select the output version's text encoding.</summary>
    /// <param name="description">The field description, or null for omission.</param>
    /// <param name="pdf20">Whether to allow PDF 2.0 UTF-8 text strings.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(null, false)]
    [Arguments("", false)]
    [Arguments("Accept terms", false)]
    [Arguments("名前", false)]
    [Arguments(null, true)]
    [Arguments("", true)]
    [Arguments("Accept terms", true)]
    [Arguments("名前", true)]
    public async Task DescriptionRoundTrips(string? description, bool pdf20, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(CreateBytes(new() { PrintField = new(null, null, description) }, pdf20), null, cancellationToken);
        var field = ReadField(document);
        var raw = field.Dictionary.GetDictionary(KnownName.A)!.Get(document.Objects.Names.Intern("Desc"u8));
        await Assert.That(field.Attributes.PrintField.Description).IsEqualTo(description);
        await Assert.That(raw.Kind).IsEqualTo(description is null ? PdfKind.Null : PdfKind.String);
        if (description is not null)
        {
            await Assert.That(raw.AsStringBytes().SequenceEqual(PdfText.Encode(description, pdf20))).IsTrue();
        }
    }

    /// <summary>An omitted field option adds no attributes and leaves the shared default unchanged.</summary>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task OmittedOptionsStayAbsent(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(CreateBytes(PdfTagOptions.Default, true), null, cancellationToken);
        await Assert.That(ReadField(document).Dictionary.Get(KnownName.A).IsNull).IsTrue();
        await Assert.That(PdfTagOptions.Default.PrintField).IsNull();
        var original = new PdfTagOptions { PrintField = new("cb", "on", FieldDescription) };
        var omitted = original with { PrintField = null };
        await Assert.That(original.PrintField).IsEqualTo(new PdfPrintFieldAttributes("cb", "on", FieldDescription));
        await Assert.That(omitted.PrintField).IsNull();
    }

    /// <summary>Optional role omission does not forbid a valid radio or checkbox state.</summary>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task StateDoesNotRequireExplicitRole(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(CreateBytes(new() { PrintField = new(null, "off", null) }, true), null, cancellationToken);
        await Assert.That(ReadField(document).Attributes.PrintField.Checked).IsEqualTo("off");
        await Assert.That(ReadField(document).Attributes.PrintField.Role).IsNull();
    }

    /// <summary>Nonstandard role names cannot be emitted by the strict creator.</summary>
    /// <param name="role">The invalid role name.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("")]
    [Arguments("CB")]
    [Arguments("PrivateRole")]
    public async Task UnknownRoleIsRejected(string role)
    {
        var tags = new PdfTagTree(new PdfDocumentBuilder());
        await Assert.That(() => tags.AddElement(tags.Root, "Form", new() { PrintField = new(role, null, null) })).Throws<ArgumentException>();
        await Assert.That(tags.Root.Kids).IsEmpty();
    }

    /// <summary>Nonstandard checked states cannot be emitted by the strict creator.</summary>
    /// <param name="state">The invalid state name.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("")]
    [Arguments("On")]
    [Arguments("PrivateState")]
    public async Task UnknownStateIsRejected(string state)
    {
        var tags = new PdfTagTree(new PdfDocumentBuilder());
        await Assert.That(() => tags.AddElement(tags.Root, "Form", new() { PrintField = new("cb", state, null) })).Throws<ArgumentException>();
        await Assert.That(tags.Root.Kids).IsEmpty();
    }

    /// <summary>PrintField attributes require a Form element.</summary>
    /// <param name="type">The unrelated structure type.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("P")]
    [Arguments("Span")]
    [Arguments("TH")]
    [Arguments("L")]
    public async Task NonFormAttributesAreRejected(string type)
    {
        var tags = new PdfTagTree(new PdfDocumentBuilder());
        await Assert.That(() => tags.AddElement(tags.Root, type, new() { PrintField = new("cb", "on", null) })).Throws<ArgumentException>();
        await Assert.That(tags.Root.Kids).IsEmpty();
    }

    /// <summary>States of known non-checkbox and non-radio roles are not written as checkbox states.</summary>
    /// <param name="role">The incompatible field role.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("pb")]
    [Arguments("tv")]
    [Arguments("lb")]
    public async Task StateOnIncompatibleRoleIsRejected(string role)
    {
        var tags = new PdfTagTree(new PdfDocumentBuilder());
        await Assert.That(() => tags.AddElement(tags.Root, "Form", new() { PrintField = new(role, "on", null) })).Throws<ArgumentException>();
        await Assert.That(tags.Root.Kids).IsEmpty();
    }

    /// <summary>PDF 1.4 cannot contain PrintField attributes introduced in PDF 1.7.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Pdf14AttributesAreRejected()
    {
        var tags = new PdfTagTree(new PdfDocumentBuilder { Version = PdfHeaderVersion.Pdf14 });
        await Assert.That(() => tags.AddElement(tags.Root, "Form", new() { PrintField = new("cb", "on", null) })).Throws<ArgumentException>();
        await Assert.That(tags.Root.Kids).IsEmpty();
    }

    /// <summary>Allowing PDF 2.0 namespaces does not permit a listbox role in PDF 1.7 output.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ListboxRequiresPdf20Output()
    {
        var tags = new PdfTagTree(new PdfDocumentBuilder { Version = PdfHeaderVersion.Pdf17 }, true);
        await Assert.That(() => tags.AddElement(tags.Root, "Form", new() { PrintField = new("lb", null, null) })).Throws<ArgumentException>();
        await Assert.That(tags.Root.Kids).IsEmpty();
    }

    /// <summary>The final output version is checked again if the builder changes after element creation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task VersionChangeCannotWriteInvalidRole()
    {
        var builder = new PdfDocumentBuilder { Version = PdfHeaderVersion.Pdf20 };
        var tags = new PdfTagTree(builder);
        _ = tags.AddElement(tags.Root, "Form", new() { PrintField = new("lb", null, null) });
        builder.Version = PdfHeaderVersion.Pdf17;
        await Assert.That(() => tags.Finish()).Throws<ArgumentException>();
    }

    /// <summary>A catalog version upgrade enables the corresponding attributes even with an older file header.</summary>
    /// <param name="version">The catalog upgrade.</param>
    /// <param name="role">The newly available role.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("1.7", "cb")]
    [Arguments("2.0", "lb")]
    public async Task BuilderCatalogUpgradeAllowsAttributes(string version, string role)
    {
        var builder = new PdfDocumentBuilder { Version = PdfHeaderVersion.Pdf14 };
        builder.CatalogEntries.Set(KnownName.Version, PdfValue.FromName(builder.Names.Intern(version)));
        var tags = new PdfTagTree(builder);
        var field = tags.AddElement(tags.Root, "Form", new() { PrintField = new(role, null, null) });
        await Assert.That(field.Options.PrintField!.Value.Role).IsEqualTo(role);
        await Assert.That(tags.Finish()).IsEqualTo(tags.RootId);
    }

    /// <summary>Edited-document sinks use both the original header and a catalog upgrade when selecting the field spelling.</summary>
    /// <param name="header">The original file header.</param>
    /// <param name="upgrade">The optional catalog version.</param>
    /// <param name="pdf20">Whether the resulting document supports PDF 2.0.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("1.7", null, false)]
    [Arguments("2.0", null, true)]
    [Arguments("1.4", "1.7", false)]
    [Arguments("1.4", "2.0", true)]
    [Arguments("1.7", "2.0", true)]
    [Arguments("2.0", "1.7", true)]
    public async Task EditedSinkUsesEffectiveVersion(string header, string? upgrade, bool pdf20, CancellationToken cancellationToken)
    {
        var builder = new PdfDocumentBuilder { Version = HeaderVersion(header) };
        if (upgrade is not null)
        {
            builder.CatalogEntries.Set(KnownName.Version, PdfValue.FromName(builder.Names.Intern(upgrade)));
        }

        _ = builder.AddSheet(PageSide, PageSide, []);
        using var document = await PdfDocumentReader.OpenAsync(builder.ToArray(), null, cancellationToken);
        var tags = new PdfTagTree(new PdfObjectStoreSink(document.Objects));
        var field = tags.AddElement(tags.Root, "Form", new() { PrintField = new("cb", "on", null) });
        _ = tags.Finish();
        var attributes = StoreReading.GetDictionary(document.Objects, field.Id)!.GetDictionary(KnownName.A)!;
        await Assert.That(attributes.Get(document.Objects.Names.Intern(pdf20 ? CheckedKey : LegacyCheckedKey)).Kind).IsEqualTo(PdfKind.Name);
        await Assert.That(attributes.Get(document.Objects.Names.Intern(pdf20 ? LegacyCheckedKey : CheckedKey)).IsNull).IsTrue();
    }

    /// <summary>An edited PDF 1.4 source needs a version upgrade before it can receive PrintField attributes.</summary>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task EditedPdf14RequiresUpgrade(CancellationToken cancellationToken)
    {
        var builder = new PdfDocumentBuilder { Version = PdfHeaderVersion.Pdf14 };
        _ = builder.AddSheet(PageSide, PageSide, []);
        using var document = await PdfDocumentReader.OpenAsync(builder.ToArray(), null, cancellationToken);
        var tags = new PdfTagTree(new PdfObjectStoreSink(document.Objects));
        await Assert.That(() => tags.AddElement(tags.Root, "Form", new() { PrintField = new("cb", null, null) })).Throws<ArgumentException>();
        await Assert.That(tags.Root.Kids).IsEmpty();
    }

    /// <summary>Independent attribute owners do not overwrite field attributes.</summary>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task OwnersRemainSeparate(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(CreateBytes(new() { PrintField = new("cb", "on", FieldDescription), NoteType = PdfNoteType.Footnote }, true), null, cancellationToken);
        var field = ReadField(document);
        var attributes = field.Dictionary.GetArray(KnownName.A)!;
        await Assert.That(attributes.Count).IsEqualTo(TwoOwners);
        await Assert.That(document.Objects.Names.GetString(attributes.Get(0).AsDictionary()!.Get(KnownName.O).AsName())).IsEqualTo("FENote");
        await Assert.That(document.Objects.Names.GetString(attributes.Get(1).AsDictionary()!.Get(KnownName.O).AsName())).IsEqualTo("PrintField");
        await Assert.That(field.Attributes.PrintField).IsEqualTo(new("cb", "on", FieldDescription));
    }

    /// <summary>New field attributes survive edited compact and incremental output.</summary>
    /// <param name="incremental">Whether to append an incremental revision.</param>
    /// <param name="cancellationToken">Cancels reading and saving.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EditedFieldsRoundTrip(bool incremental, CancellationToken cancellationToken)
    {
        var expected = new PdfPrintFieldAttributes("cb", "neutral", "名前");
        using var document = await PdfDocumentReader.OpenAsync(CreateBytes(new() { PrintField = expected }, true), null, cancellationToken);
        var field = ReadField(document);
        var edited = field.Dictionary.Clone();
        edited.Set(KnownName.T, PdfValue.FromString("edited field"u8.ToArray()));
        StoreEditing.Replace(document.Objects, field.Id, PdfValue.FromDictionary(edited));
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
        await Assert.That(ReadField(saved).Title).IsEqualTo("edited field");
        await Assert.That(ReadField(saved).Attributes.PrintField).IsEqualTo(expected);
    }

    /// <summary>Created fields retain their attributes through PDF/A conversion and every saved output passes veraPDF.</summary>
    /// <param name="level">The compatible PDF/A level.</param>
    /// <param name="flavour">The independent validator profile.</param>
    /// <param name="pdf20">Whether the source uses PDF 2.0 text strings.</param>
    /// <param name="cancellationToken">Cancels conversion, saving and validation.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfALevel.PdfA2B, "2b", false)]
    [Arguments(PdfALevel.PdfA4, "4", true)]
    [NotInParallel(nameof(VeraPdfValidator))]
    public async Task CompatiblePdfAOutputPassesVeraPdf(PdfALevel level, string flavour, bool pdf20, CancellationToken cancellationToken)
    {
        var home = VeraPdfValidator.RequireHome();
        using var folder = new PdfAOutputFolder($"printfield-{flavour}");
        var source = Path.Combine(folder.Path, "source.pdf");
        var expected = new PdfPrintFieldAttributes("cb", "on", "名前");
        using (var document = await PdfDocumentReader.OpenAsync(CreateBytes(new() { PrintField = expected }, pdf20), null, cancellationToken))
        {
            await using var output = File.Create(source);
            var result = await PdfAConverter.SaveAsPdfAAsync(document, level, output, cancellationToken);
            await Assert.That(result.Converted).IsTrue().Because(string.Join("; ", result.Problems.Select(static problem => problem.Description)));
        }

        var outputs = await PdfAOutputs.WriteAllAsync(source, folder.Path, cancellationToken);
        outputs["source"] = source;
        var jobs = await VeraPdfValidator.ValidateAsync(home, flavour, [.. outputs.Values], cancellationToken);
        foreach (var path in outputs.Values)
        {
            var job = jobs[Path.GetFullPath(path)];
            await Assert.That(job.IsCompliant).IsTrue().Because(job.FailureSummary);
            using var saved = await PdfDocumentReader.OpenAsync(await File.ReadAllBytesAsync(path, cancellationToken), null, cancellationToken);
            await Assert.That(ReadField(saved).Attributes.PrintField).IsEqualTo(expected);
        }
    }

    /// <summary>Creates marked field content with no accessibility conformance claim.</summary>
    /// <param name="options">The field attributes.</param>
    /// <param name="pdf20">Whether to write PDF 2.0 instead of PDF 1.7.</param>
    /// <returns>The complete file bytes.</returns>
    private static byte[] CreateBytes(PdfTagOptions options, bool pdf20)
    {
        var creator = new PdfDocumentCreator(new() { Tagged = true });
        creator.Builder.Version = pdf20 ? PdfHeaderVersion.Pdf20 : PdfHeaderVersion.Pdf17;
        var tags = creator.Tags!;
        var field = tags.AddElement(tags.Root, "Form", options);
        using (var page = creator.BeginPage(PageSide, PageSide))
        {
            page.BeginContent(field);
            page.FillRectangle(0, 0, 0, FieldSide, FieldSide, FieldSide, FieldSide);
            page.EndContent();
            _ = page.Finish();
        }

        return creator.ToArray();
    }

    /// <summary>Returns the generated Form structure element.</summary>
    /// <param name="document">The open document.</param>
    /// <returns>The field element.</returns>
    private static PdfStructureElement ReadField(PdfDocument document) =>
        PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;

    /// <summary>Maps the explicitly requested fixture header to the builder's supported versions.</summary>
    /// <param name="version">The fixture header.</param>
    /// <returns>The supported header version.</returns>
    private static PdfHeaderVersion HeaderVersion(string version) => version switch
    {
        "1.4" => PdfHeaderVersion.Pdf14,
        "1.7" => PdfHeaderVersion.Pdf17,
        _ => PdfHeaderVersion.Pdf20,
    };
}
