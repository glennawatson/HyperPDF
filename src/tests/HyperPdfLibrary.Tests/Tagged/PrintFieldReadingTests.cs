// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Tagged;

/// <summary>Checks non-interactive field attributes, including damaged values and legacy spelling.</summary>
public sealed class PrintFieldReadingTests
{
    /// <summary>PrintField attributes follow standard owner exclusions and equivalent namespace names.</summary>
    /// <param name="owner">The owner entries.</param>
    /// <param name="accepted">Whether this owner permits non-interactive field attributes.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("/O /PrintField", true)]
    [Arguments("", true)]
    [Arguments("/O null", true)]
    [Arguments("/O /Private", true)]
    [Arguments("/O /FENote", true)]
    [Arguments("/O /NSO /NS << /NS (PrintField) >>", true)]
    [Arguments("/O /NSO /NS << /NS <feff005000720069006e0074004600690065006c0064> >>", true)]
    [Arguments("/O /NSO /NS << /NS <fffe5000720069006e0074004600690065006c006400> >>", true)]
    [Arguments("/O /NSO /NS << /NS <efbbbf5072696e744669656c64> >>", true)]
    [Arguments("/O /NSO /NS << /NS (https://example.test/field-attributes) >>", true)]
    [Arguments("/O /Layout", false)]
    [Arguments("/O /Table", false)]
    [Arguments("/O /List", false)]
    [Arguments("/O /Artifact", false)]
    [Arguments("/O /NSO /NS << /NS (Layout) >>", false)]
    [Arguments("/O /NSO /NS << /NS (Table) >>", false)]
    [Arguments("/O /NSO /NS << /NS (List) >>", false)]
    [Arguments("/O /NSO /NS << /NS (Artifact) >>", false)]
    [Arguments("/O /NSO", false)]
    [Arguments("/O /NSO /NS << /NS () >>", false)]
    [Arguments("/O /NSO /NS << /NS 1 >>", false)]
    [Arguments("/O 1", false)]
    public async Task AttributesRespectOwner(string owner, bool accepted, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(
            Build($"/A << {owner} /Role /cb /Checked /on /Desc (Accept terms) >>", string.Empty, string.Empty),
            null,
            cancellationToken);
        var field = GetField(document).Attributes.PrintField;
        await Assert.That(field.Role).IsEqualTo(accepted ? "cb" : null);
        await Assert.That(field.Checked).IsEqualTo(accepted ? "on" : null);
        await Assert.That(field.Description).IsEqualTo(accepted ? "Accept terms" : null);
    }

    /// <summary>Every standard role and private names remain available without blocking a damaged document.</summary>
    /// <param name="role">The source role name.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("rb")]
    [Arguments("cb")]
    [Arguments("pb")]
    [Arguments("tv")]
    [Arguments("lb")]
    [Arguments("PrivateRole")]
    public async Task RetainsRoleNames(string role, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(
            Build($"/A << /O /PrintField /Role /{role} >>", string.Empty, string.Empty),
            null,
            cancellationToken);
        await Assert.That(GetField(document).Attributes.PrintField.Role).IsEqualTo(role);
    }

    /// <summary>Legacy and canonical keys remain readable; a valid canonical value wins when both are present.</summary>
    /// <param name="entries">The source checked-state entries.</param>
    /// <param name="expected">The retained state, or null for malformed values.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("/checked /on", "on")]
    [Arguments("/Checked /on", "on")]
    [Arguments("/Checked /off", "off")]
    [Arguments("/Checked /neutral", "neutral")]
    [Arguments("/Checked /PrivateState", "PrivateState")]
    [Arguments("/checked /on /Checked /off", "off")]
    [Arguments("/Checked /off /checked /on", "off")]
    [Arguments("/Checked 1 /checked /on", "on")]
    [Arguments("/Checked null /checked /neutral", "neutral")]
    [Arguments("/Checked (on)", null)]
    [Arguments("/checked true", null)]
    [Arguments("", null)]
    public async Task ReadsCheckedSpelling(string entries, string? expected, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(
            Build($"/A << /O /PrintField {entries} >>", string.Empty, string.Empty),
            null,
            cancellationToken);
        await Assert.That(GetField(document).Attributes.PrintField.Checked).IsEqualTo(expected);
    }

    /// <summary>Descriptions use text-string decoding and retain an explicit empty string.</summary>
    /// <param name="value">The Desc entry's PDF value.</param>
    /// <param name="expected">The decoded description, or null when not a text string.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("(Name)", "Name")]
    [Arguments("<feff004e0061006d0065>", "Name")]
    [Arguments("<efbbbf4e616d65>", "Name")]
    [Arguments("()", "")]
    [Arguments("/Name", null)]
    [Arguments("1", null)]
    [Arguments("null", null)]
    public async Task DecodesDescription(string value, string? expected, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(
            Build($"/A << /O /PrintField /Desc {value} >>", string.Empty, string.Empty),
            null,
            cancellationToken);
        await Assert.That(GetField(document).Attributes.PrintField.Description).IsEqualTo(expected);
    }

    /// <summary>Own attributes override ordered class values while malformed entries permit an earlier valid value.</summary>
    /// <param name="entries">The field's own entries.</param>
    /// <param name="role">The effective role name.</param>
    /// <param name="state">The effective checked state.</param>
    /// <param name="description">The effective alternate field name.</param>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("", "cb", "off", "Last")]
    [Arguments("/A << /O /PrintField /Role /tv /Checked /neutral /Desc () >>", "tv", "neutral", "")]
    [Arguments("/A << /O /PrintField /Role 1 /Checked false /Desc /bad >>", "cb", "off", "Last")]
    [Arguments("/A [<< /O /PrintField /Role /rb /Checked /on /Desc (First) >> 0 << /O /PrintField /Role /tv /checked /neutral /Desc (Own) >> 1]", "tv", "neutral", "Own")]
    public async Task AppliesPrecedence(string entries, string role, string state, string description, CancellationToken cancellationToken)
    {
        const string classes = "/ClassMap << /First << /O /PrintField /Role /rb /checked /on /Desc (First) >> /Last << /O /PrintField /Role /cb /Checked /off /Desc (Last) >> >>";
        using var document = await PdfDocumentReader.OpenAsync(Build($"{entries} /C [/First /Last]", string.Empty, classes), null, cancellationToken);
        await Assert.That(GetField(document).Attributes.PrintField).IsEqualTo(new(role, state, description));
    }

    /// <summary>The optional attributes do not inherit from the field's parent or leak from the previous element.</summary>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task DoesNotInheritAttributes(CancellationToken cancellationToken)
    {
        const string ancestor = "/A << /O /PrintField /Role /cb /Checked /on /Desc (Parent) >>";
        using var document = await PdfDocumentReader.OpenAsync(Build(string.Empty, ancestor, string.Empty), null, cancellationToken);
        var field = GetField(document);
        await Assert.That(field.Parent!.Attributes.PrintField).IsEqualTo(new("cb", "on", "Parent"));
        await Assert.That(field.Attributes.PrintField).IsEqualTo(default(PdfPrintFieldAttributes));
    }

    /// <summary>Tagged page reading nodes retain the non-interactive field's semantic attributes.</summary>
    /// <param name="cancellationToken">Cancels reading.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task TaggedNodesExposeFieldAttributes(CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(
            MiniPdf.Build(
            [
                "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 3 0 R >>",
                "<< /Type /Pages /Count 1 /Kids [6 0 R] >>",
                "<< /Type /StructTreeRoot /K 4 0 R >>",
                "<< /Type /StructElem /S /Document /P 3 0 R /K 5 0 R >>",
                "<< /Type /StructElem /S /Form /P 4 0 R /Pg 6 0 R /ActualText (Checked) /A << /O /PrintField /Role /cb /Checked /on /Desc (Accept terms) >> >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 1 1] /Resources <<>> >>",
            ]),
            null,
            cancellationToken);
        var page = PdfReadingStructure.ReadTagged(document, 0);
        var field = page.Nodes.Single().Children.Single();
        await Assert.That(field.Role).IsEqualTo(PdfSemanticRole.FormField);
        await Assert.That(field.Element!.Attributes.PrintField).IsEqualTo(new("cb", "on", "Accept terms"));
    }

    /// <summary>Explicit cancellation is observed even when an element has no attributes to decode.</summary>
    /// <param name="specified">Whether the source has a PrintField attribute object.</param>
    /// <param name="cancellationToken">Cancels source reading.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task AttributeDecodingHonoursCancellation(bool specified, CancellationToken cancellationToken)
    {
        var entries = specified ? "/A << /O /PrintField /Role /cb >>" : string.Empty;
        using var document = await PdfDocumentReader.OpenAsync(Build(entries, string.Empty, string.Empty), null, cancellationToken);
        var names = document.Objects.Names;
        var reader = new StructureAttributeReader(new TaggedNames(names), names, null);
        using var cancellation = new CancellationTokenSource();
        await cancellation.CancelAsync();
        var field = GetField(document);
        await Assert.That(() => reader.Read(field.Dictionary, cancellation.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>Both save modes preserve source field semantics and the legacy spelling after an element edit.</summary>
    /// <param name="incremental">Whether to append a revision.</param>
    /// <param name="cancellationToken">Cancels reading and saving.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task EditedFieldsPreserveAttributes(bool incremental, CancellationToken cancellationToken)
    {
        const string attributes = "/A << /O /PrintField /Role /cb /checked /on /Desc <feff004e0061006d0065> >>";
        using var document = await PdfDocumentReader.OpenAsync(Build(attributes, string.Empty, string.Empty), null, cancellationToken);
        var field = GetField(document);
        var edited = field.Dictionary.Clone();
        edited.Set(KnownName.T, PdfValue.FromString("edited"u8.ToArray()));
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
        var savedField = GetField(saved);
        await Assert.That(savedField.Attributes.PrintField).IsEqualTo(new("cb", "on", "Name"));
        await Assert.That(savedField.Title).IsEqualTo("edited");
        var dictionary = savedField.Dictionary.GetDictionary(KnownName.A)!;
        await Assert.That(dictionary.Get(saved.Objects.Names.Intern("checked"u8)).Kind).IsEqualTo(PdfKind.Name);
        await Assert.That(Convert.ToHexString(dictionary.GetStringBytes(KnownName.Desc))).IsEqualTo("FEFF004E0061006D0065");
    }

    /// <summary>Gets the non-interactive field from the synthetic structure tree.</summary>
    /// <param name="document">The open document.</param>
    /// <returns>The Form structure element.</returns>
    private static PdfStructureElement GetField(PdfDocument document) =>
        PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!;

    /// <summary>Builds exact field, parent and class attributes without requiring fonts or local files.</summary>
    /// <param name="fieldEntries">The field's entries.</param>
    /// <param name="ancestorEntries">The parent element's entries.</param>
    /// <param name="rootEntries">The structure root's entries.</param>
    /// <returns>The file bytes.</returns>
    private static byte[] Build(string fieldEntries, string ancestorEntries, string rootEntries) => MiniPdf.Build(
    [
        "<< /Type /Catalog /Pages 2 0 R /StructTreeRoot 3 0 R >>",
        "<< /Type /Pages /Count 1 /Kids [6 0 R] >>",
        $"<< /Type /StructTreeRoot /K 4 0 R {rootEntries} >>",
        $"<< /Type /StructElem /S /Document /P 3 0 R /K 5 0 R {ancestorEntries} >>",
        $"<< /Type /StructElem /S /Form /P 4 0 R {fieldEntries} >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 1 1] /Resources <<>> >>",
    ]);
}
