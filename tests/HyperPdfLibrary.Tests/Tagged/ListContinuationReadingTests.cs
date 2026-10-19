// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Tagged;

/// <summary>Checks non-inherited list continuation flags and byte-string identities.</summary>
public sealed class ListContinuationReadingTests
{
    /// <summary>The byte-string identifier, including bytes that are not text.</summary>
    private const string PreviousId = "00FF3132";

    /// <summary>Continuation attributes are decoded only for permitted owners.</summary>
    /// <param name="owner">The attribute owner's PDF entries.</param>
    /// <param name="accepted">Whether the owner permits list attributes.</param>
    /// <param name="cancellationToken">Cancels document reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("/O /List", true)]
    [Arguments("", true)]
    [Arguments("/O /Private", true)]
    [Arguments("/O /FENote", true)]
    [Arguments("/O /NSO /NS << /NS (List) >>", true)]
    [Arguments("/O /NSO /NS << /NS <feff004c006900730074> >>", true)]
    [Arguments("/O /NSO /NS << /NS (https://example.test/list-attributes) >>", true)]
    [Arguments("/O /NSO /NS << /NS (Private) >>", true)]
    [Arguments("/O /Layout", false)]
    [Arguments("/O /Table", false)]
    [Arguments("/O /PrintField", false)]
    [Arguments("/O /Artifact", false)]
    [Arguments("/O /NSO /NS << /NS (Layout) >>", false)]
    [Arguments("/O /NSO /NS << /NS (Table) >>", false)]
    [Arguments("/O /NSO /NS << /NS (PrintField) >>", false)]
    [Arguments("/O /NSO /NS << /NS (Artifact) >>", false)]
    [Arguments("/O /NSO /NS << /NS /List >>", false)]
    [Arguments("/O /NSO /NS << /NS () >>", false)]
    [Arguments("/O /NSO /NS << /NS <feff> >>", false)]
    [Arguments("/O /NSO /NS << /NS <efbbbf> >>", false)]
    [Arguments("/O /NSO", false)]
    [Arguments("/O 1", false)]
    public async Task ReadsPermittedOwners(string owner, bool accepted, CancellationToken cancellationToken)
    {
        var attributes = $"/A << {owner} /ContinuedList true /ContinuedFrom <00ff3132> /ListNumbering /Ordered >>";
        using var document = await PdfDocumentReader.OpenAsync(Build(attributes, string.Empty, string.Empty, string.Empty), null, cancellationToken);
        var list = GetList(document);

        await Assert.That(list.Attributes.ContinuedList).IsEqualTo(accepted ? true : (bool?)null);
        await Assert.That(list.Attributes.HasContinuedFrom).IsEqualTo(accepted);
        await Assert.That(Convert.ToHexString(list.Attributes.ContinuedFromId)).IsEqualTo(accepted ? PreviousId : string.Empty);
        await Assert.That(list.Attributes.ListNumbering).IsEqualTo(accepted ? "Ordered" : string.Empty);
    }

    /// <summary>Absent, false and malformed flags are distinguished without rejecting the file.</summary>
    /// <param name="value">The optional flag entry.</param>
    /// <param name="expected">The effective flag, or null for no valid value.</param>
    /// <param name="cancellationToken">Cancels document reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("", null)]
    [Arguments("/ContinuedList false", false)]
    [Arguments("/ContinuedList true", true)]
    [Arguments("/ContinuedList null", null)]
    [Arguments("/ContinuedList 1", null)]
    [Arguments("/ContinuedList /true", null)]
    [Arguments("/ContinuedList (true)", null)]
    public async Task ReadsOnlyBooleanFlags(string value, bool? expected, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build($"/A << /O /List {value} >>", string.Empty, string.Empty, string.Empty), null, cancellationToken);
        await Assert.That(GetList(document).Attributes.ContinuedList).IsEqualTo(expected);
    }

    /// <summary>Previous-list identities retain their exact bytes, including empty and text-prefixed strings.</summary>
    /// <param name="value">The PDF string spelling.</param>
    /// <param name="expected">The expected hexadecimal bytes.</param>
    /// <param name="cancellationToken">Cancels document reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("<00ff3132>", PreviousId)]
    [Arguments("(previous)", "70726576696F7573")]
    [Arguments("<feff000000ff>", "FEFF000000FF")]
    [Arguments("<efbbbf6162>", "EFBBBF6162")]
    [Arguments("<>", "")]
    public async Task RetainsByteStringIdentity(string value, string expected, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build($"/A << /O /List /ContinuedFrom {value} >>", string.Empty, string.Empty, string.Empty), null, cancellationToken);
        var attributes = GetList(document).Attributes;
        await Assert.That(attributes.HasContinuedFrom).IsTrue();
        await Assert.That(Convert.ToHexString(attributes.ContinuedFromId)).IsEqualTo(expected);
        await Assert.That(attributes.ContinuedList).IsNull();
    }

    /// <summary>Malformed identity values remain readable but are not mistaken for byte strings.</summary>
    /// <param name="value">The malformed PDF value.</param>
    /// <param name="cancellationToken">Cancels document reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("null")]
    [Arguments("/previous")]
    [Arguments("true")]
    [Arguments("1")]
    [Arguments("[]")]
    [Arguments("<<>>")]
    public async Task MalformedIdentityRemainsReadable(string value, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(Build($"/A << /O /List /ContinuedFrom {value} >>", string.Empty, string.Empty, string.Empty), null, cancellationToken);
        var attributes = GetList(document).Attributes;
        await Assert.That(attributes.HasContinuedFrom).IsFalse();
        await Assert.That(attributes.ContinuedFromId.IsEmpty).IsTrue();
    }

    /// <summary>Own and later attributes take precedence independently for the flag and identity.</summary>
    /// <param name="attributes">The element's attribute objects.</param>
    /// <param name="classes">Its optional class references.</param>
    /// <param name="expectedId">The expected previous-list identity.</param>
    /// <param name="cancellationToken">Cancels document reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments("/A [<< /O /List /ContinuedList true /ContinuedFrom (first) >> 0 << /O /List /ContinuedList false /ContinuedFrom (last) >> 1]", "", "6C617374")]
    [Arguments("/A << /O /List /ContinuedList false /ContinuedFrom (own) >>", "/C [/First /Last]", "6F776E")]
    [Arguments("/A << /O /List /ContinuedList false /ContinuedFrom <> >>", "/C [/First /Last]", "")]
    [Arguments("/A << /O /List /ContinuedList false >>", "/C [/First /Last]", "6C617374")]
    [Arguments("/A << /O /List /ContinuedList (bad) /ContinuedFrom 1 >>", "/C [/First /Last]", "6C617374")]
    public async Task ResolvesPerAttributePrecedence(string attributes, string classes, string expectedId, CancellationToken cancellationToken)
    {
        const string classMap = "/ClassMap << /First << /O /List /ContinuedList true /ContinuedFrom (first) >> /Last << /O /List /ContinuedList false /ContinuedFrom (last) >> >>";
        using var document = await PdfDocumentReader.OpenAsync(Build(attributes, string.Empty, classes, classMap), null, cancellationToken);
        var list = GetList(document).Attributes;
        await Assert.That(list.ContinuedList).IsFalse();
        await Assert.That(Convert.ToHexString(list.ContinuedFromId)).IsEqualTo(expectedId);
    }

    /// <summary>Continuation values are not inherited from ancestors or retained from the reader's previous element.</summary>
    /// <param name="cancellationToken">Cancels document reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task DoesNotInheritContinuation(CancellationToken cancellationToken)
    {
        const string ancestor = "/A << /O /List /ContinuedList true /ContinuedFrom (ancestor) /ListNumbering /Decimal >>";
        using var document = await PdfDocumentReader.OpenAsync(Build(string.Empty, ancestor, string.Empty, string.Empty), null, cancellationToken);
        var list = GetList(document);
        await Assert.That(list.Parent!.Attributes.ContinuedList).IsTrue();
        await Assert.That(list.Attributes.ContinuedList).IsNull();
        await Assert.That(list.Attributes.HasContinuedFrom).IsFalse();
        await Assert.That(list.Attributes.ContinuedFromId.IsEmpty).IsTrue();
    }

    /// <summary>Malformed later objects do not hide earlier correctly typed values.</summary>
    /// <param name="cancellationToken">Cancels document reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task MalformedLaterValuesUseEarlierAttributes(CancellationToken cancellationToken)
    {
        const string attributes = "/A [<< /O /List /ContinuedList true /ContinuedFrom <00ff3132> >> << /O /List /ContinuedList 1 /ContinuedFrom /bad >>]";
        using var document = await PdfDocumentReader.OpenAsync(Build(attributes, string.Empty, string.Empty, string.Empty), null, cancellationToken);
        var list = GetList(document).Attributes;
        await Assert.That(list.ContinuedList).IsTrue();
        await Assert.That(Convert.ToHexString(list.ContinuedFromId)).IsEqualTo(PreviousId);
    }

    /// <summary>Retained attribute snapshots do not change when a reusable reader decodes another revision.</summary>
    /// <param name="cancellationToken">Cancels document reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task RetainedSnapshotsStayIndependent(CancellationToken cancellationToken)
    {
        const string attributes = "/A << /O /List /ContinuedList true /ContinuedFrom <00ff3132> >>";
        using var document = await PdfDocumentReader.OpenAsync(Build(attributes, string.Empty, string.Empty, string.Empty), null, cancellationToken);
        var list = GetList(document);
        var names = document.Objects.Names;
        var reader = new StructureAttributeReader(new TaggedNames(names), names, null);
        var original = reader.Read(list.Dictionary, cancellationToken);
        var changed = list.Dictionary.Clone();
        var changedAttributes = changed.GetDictionary(KnownName.A)!.Clone();
        changedAttributes.Set(names.Intern("ContinuedList"u8), PdfValue.FromBoolean(false));
        changedAttributes.Set(names.Intern("ContinuedFrom"u8), PdfValue.FromString("next"u8.ToArray()));
        changed.Set(KnownName.A, PdfValue.FromDictionary(changedAttributes));
        var revision = reader.Read(changed, cancellationToken);

        await Assert.That(original.ContinuedList).IsTrue();
        await Assert.That(Convert.ToHexString(original.ContinuedFromId)).IsEqualTo(PreviousId);
        await Assert.That(revision.ContinuedList).IsFalse();
        await Assert.That(Convert.ToHexString(revision.ContinuedFromId)).IsEqualTo("6E657874");
    }

    /// <summary>Page reading nodes retain the list's continuation attributes.</summary>
    /// <param name="cancellationToken">Cancels document reading.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    public async Task TaggedNodeExposesContinuation(CancellationToken cancellationToken)
    {
        const string attributes = "/A << /O /List /ContinuedList true /ContinuedFrom <00ff3132> >>";
        using var document = await PdfDocumentReader.OpenAsync(Build(attributes, string.Empty, string.Empty, string.Empty), null, cancellationToken);
        var page = PdfReadingStructure.ReadTagged(document, 0);
        var list = page.Nodes.Single().Children.Single().Children.Single();
        await Assert.That(list.Role).IsEqualTo(PdfSemanticRole.List);
        await Assert.That(list.Element!.Attributes.ContinuedList).IsTrue();
        await Assert.That(Convert.ToHexString(list.Element.Attributes.ContinuedFromId)).IsEqualTo(PreviousId);
    }

    /// <summary>Both save formats retain continuation byte strings when their structure element is edited.</summary>
    /// <param name="incremental">Whether to append an object revision.</param>
    /// <param name="cancellationToken">Cancels saving and reopening.</param>
    /// <returns>The assertion task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task SavesPreserveContinuation(bool incremental, CancellationToken cancellationToken)
    {
        using var document = await PdfDocumentReader.OpenAsync(BuildForSaving(), null, cancellationToken);
        var list = GetList(document);
        var edited = list.Dictionary.Clone();
        edited.Set(KnownName.T, PdfValue.FromString("changed list"u8.ToArray()));
        StoreEditing.Replace(document.Objects, list.Id, PdfValue.FromDictionary(edited));
        await using var output = new MemoryStream();
        var report = new PdfSaveReport();
        if (incremental)
        {
            await PdfIncrementalWriter.SaveAsync(document.Objects, output, report, cancellationToken);
        }
        else
        {
            await PdfCompactWriter.SaveAsync(document.Objects, PdfCompactOptions.Default, output, report, cancellationToken);
        }

        using var reopened = await PdfDocumentReader.OpenAsync(output.ToArray(), null, cancellationToken);
        var saved = GetList(reopened);
        await Assert.That(saved.Title).IsEqualTo("changed list");
        await Assert.That(saved.Attributes.ContinuedList).IsTrue();
        await Assert.That(Convert.ToHexString(saved.Attributes.ContinuedFromId)).IsEqualTo(PreviousId);
        var previous = saved.Parent!.Kids[0].Element!;
        await Assert.That(Convert.ToHexString(previous.Dictionary.GetStringBytes(KnownName.ID))).IsEqualTo(PreviousId);
        await Assert.That(previous.Role).IsEqualTo(PdfSemanticRole.List);
    }

    /// <summary>Gets the nested list in the generated document.</summary>
    /// <param name="document">The generated document.</param>
    /// <returns>The list element.</returns>
    private static PdfStructureElement GetList(PdfDocument document) =>
        PdfDocumentTagged.GetStructureTree(document)!.Roots[0].Kids[0].Element!.Kids[^1].Element!;

    /// <summary>Builds two linked list segments with a byte-string ID and the corresponding ID-tree entry.</summary>
    /// <returns>The PDF bytes.</returns>
    private static byte[] BuildForSaving() => MiniPdf.Build(
    [
        "<< /Type /Catalog /Version /2.0 /Pages 2 0 R /StructTreeRoot 3 0 R >>",
        "<< /Type /Pages /Count 1 /Kids [7 0 R] >>",
        "<< /Type /StructTreeRoot /K 4 0 R /IDTree << /Names [<00ff3132> 8 0 R] >> >>",
        "<< /Type /StructElem /S /Document /P 3 0 R /K 5 0 R >>",
        "<< /Type /StructElem /S /Div /P 4 0 R /K [8 0 R 6 0 R] >>",
        "<< /Type /StructElem /S /L /P 5 0 R /Pg 7 0 R /K 10 0 R /Ref [8 0 R 6 0 R] /A << /O /List /ContinuedList true /ContinuedFrom <00ff3132> >> >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] >>",
        "<< /Type /StructElem /S /L /P 5 0 R /Pg 7 0 R /K 9 0 R /ID <00ff3132> /Ref [8 0 R 6 0 R] >>",
        "<< /Type /StructElem /S /LI /P 8 0 R /K 11 0 R >>",
        "<< /Type /StructElem /S /LI /P 6 0 R /K 12 0 R >>",
        "<< /Type /StructElem /S /LBody /P 9 0 R /Pg 7 0 R /ActualText (previous item) >>",
        "<< /Type /StructElem /S /LBody /P 10 0 R /Pg 7 0 R /ActualText (continued item) >>",
    ]);

    /// <summary>Builds a page with a list, an ancestor and optional attribute classes.</summary>
    /// <param name="attributes">The list's attribute entries.</param>
    /// <param name="ancestor">The ancestor's attribute entries.</param>
    /// <param name="classes">The list's optional class references.</param>
    /// <param name="classMap">The root's optional class map.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] Build(string attributes, string ancestor, string classes, string classMap) => MiniPdf.Build(
    [
        "<< /Type /Catalog /Version /2.0 /Pages 2 0 R /StructTreeRoot 3 0 R >>",
        "<< /Type /Pages /Count 1 /Kids [7 0 R] >>",
        $"<< /Type /StructTreeRoot /K 4 0 R {classMap} >>",
        "<< /Type /StructElem /S /Document /P 3 0 R /K 5 0 R >>",
        $"<< /Type /StructElem /S /Div /P 4 0 R /K 6 0 R {ancestor} >>",
        $"<< /Type /StructElem /S /L /P 5 0 R /Pg 7 0 R /ActualText (item) {attributes} {classes} >>",
        "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 300 300] >>",
    ]);
}
