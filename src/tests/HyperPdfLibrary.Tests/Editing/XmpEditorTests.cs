// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests for <see cref="XmpEditor"/>: UTF-16 packets, language alternatives and unchanged packets.</summary>
public sealed class XmpEditorTests
{
    /// <summary>The title the sample packet starts with.</summary>
    private const string OldTitle = "Old Title";

    /// <summary>The title the edits write.</summary>
    private const string NewTitle = "New Title";

    /// <summary>The German title kept by edits that do not name German.</summary>
    private const string GermanTitle = "Alter Titel";

    /// <summary>The French title.</summary>
    private const string FrenchTitle = "Ancien Titre";

    /// <summary>The packet text: a wrapper, a three-language title and padding.</summary>
    private const string Packet =
        "<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>\n"
        + "<x:xmpmeta xmlns:x=\"adobe:ns:meta/\"><rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\">"
        + "<rdf:Description rdf:about=\"\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\">"
        + "<dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">Old Title</rdf:li><rdf:li xml:lang=\"de\">Alter Titel</rdf:li>"
        + "<rdf:li xml:lang=\"fr\">Ancien Titre</rdf:li></rdf:Alt></dc:title>"
        + "</rdf:Description></rdf:RDF></x:xmpmeta>\n"
        + "                \n"
        + "<?xpacket end=\"w\"?>";

    /// <summary>A UTF-16 packet, with or without a byte order mark, is edited and written back in the same encoding.</summary>
    /// <param name="bigEndian">Whether the packet is big-endian.</param>
    /// <param name="withMark">Whether the packet starts with a byte order mark.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true, true)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(false, false)]
    public async Task Utf16PacketIsEditedInItsOwnEncoding(bool bigEndian, bool withMark)
    {
        var encoding = bigEndian ? Encoding.BigEndianUnicode : Encoding.Unicode;
        byte[] packet = [.. withMark ? encoding.GetPreamble() : [], .. encoding.GetBytes(Packet)];

        var result = XmpEditor.Apply(packet, [new(XmpProperty.Title, NewTitle)]);

        await Assert.That(result).IsNotNull();
        var text = encoding.GetString(result!);
        await Assert.That(result!.AsSpan(0, encoding.GetPreamble().Length * (withMark ? 1 : 0)).SequenceEqual(packet.AsSpan(0, withMark ? encoding.GetPreamble().Length : 0))).IsTrue();
        await Assert.That(XmpEditor.IsWellFormed(result)).IsTrue();
        await Assert.That(text).Contains(">New Title</rdf:li>");
        await Assert.That(text).DoesNotContain(">Old Title<");
        await Assert.That(text).Contains(GermanTitle);
        await Assert.That(text).Contains("<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?>");
        await Assert.That(text).EndsWith("<?xpacket end=\"w\"?>");
    }

    /// <summary>An unchanged UTF-16 or UTF-8 packet gives no result, so it is never rewritten.</summary>
    /// <param name="encodingName">The encoding: utf8, utf16be or utf16le.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("utf8")]
    [Arguments("utf16be")]
    [Arguments("utf16le")]
    public async Task UnchangedPacketIsNotRewritten(string encodingName)
    {
        var encoding = encodingName switch
        {
            "utf16be" => Encoding.BigEndianUnicode,
            "utf16le" => Encoding.Unicode,
            _ => Encoding.UTF8,
        };
        byte[] packet = [.. encoding.GetPreamble(), .. encoding.GetBytes(Packet)];

        await Assert.That(XmpEditor.Apply(packet, [new(XmpProperty.Title, OldTitle)])).IsNull();
        await Assert.That(XmpEditor.Apply(packet, [])).IsNull();
    }

    /// <summary>Setting the title replaces x-default only and keeps the other languages.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OtherLanguagesAreKept()
    {
        var result = XmpEditor.Apply(ToUtf8(Packet), [new(XmpProperty.Title, NewTitle)]);

        var text = Encoding.UTF8.GetString(result!);
        await Assert.That(text).Contains("<rdf:li xml:lang=\"x-default\">New Title</rdf:li>");
        await Assert.That(text).Contains("<rdf:li xml:lang=\"de\">Alter Titel</rdf:li>");
        await Assert.That(text).Contains("<rdf:li xml:lang=\"fr\">Ancien Titre</rdf:li>");
    }

    /// <summary>A change that names a language replaces x-default and that language, and no other.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NamedLanguageIsReplacedWithDefault()
    {
        var result = XmpEditor.Apply(ToUtf8(Packet), [new(XmpProperty.Title, NewTitle, "FR")]);

        var text = Encoding.UTF8.GetString(result!);
        await Assert.That(text).Contains("<rdf:li xml:lang=\"x-default\">New Title</rdf:li>");
        await Assert.That(text).Contains("<rdf:li xml:lang=\"fr\">New Title</rdf:li>");
        await Assert.That(text).Contains("<rdf:li xml:lang=\"de\">Alter Titel</rdf:li>");
        await Assert.That(text).DoesNotContain(FrenchTitle);
    }

    /// <summary>An alternative without an x-default entry gets one first, next to the languages it has.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingDefaultIsAdded()
    {
        var packet = Packet.Replace("<rdf:li xml:lang=\"x-default\">Old Title</rdf:li>", string.Empty, StringComparison.Ordinal);

        var result = XmpEditor.Apply(ToUtf8(packet), [new(XmpProperty.Title, NewTitle)]);

        var text = Encoding.UTF8.GetString(result!);
        await Assert.That(text).Contains("<rdf:Alt><rdf:li xml:lang=\"x-default\">New Title</rdf:li><rdf:li xml:lang=\"de\">");
    }

    /// <summary>Setting a title equal to the current one leaves the document's XMP stream byte for byte.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnchangedMetadataKeepsTheStreamBytes()
    {
        using var document = PdfDocumentReader.Open(EditingTestDocuments.CreateStructured(), null);
        var before = document.Catalog.GetStream(KnownName.Metadata)!.DecodeToArray();

        PdfDocumentMetadataEditing.SetMetadata(document, new() { Title = OldTitle });

        await Assert.That(document.Catalog.GetStream(KnownName.Metadata)!.DecodeToArray()).IsEquivalentTo(before);
    }

    /// <summary>Encodes text as UTF-8.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The bytes.</returns>
    private static byte[] ToUtf8(string text) => Encoding.UTF8.GetBytes(text);
}
