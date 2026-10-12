// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Metadata;

namespace HyperPdfLibrary.Tests.Editing;

/// <summary>Tests that UTF-32 XMP packets, which PDF/A test files use, are read and edited in their own encoding.</summary>
public sealed class XmpWideEncodingTests
{
    /// <summary>The title the edit writes, with a character outside the basic plane.</summary>
    private const string NewTitle = "Nouveau titre \U0001F4C4";

    /// <summary>Gets the packet before the edit.</summary>
    private static string Packet => "<?xpacket begin=\"﻿\" id=\"W5M0MpCehiHzreSzNTczkc9d\"?><x:xmpmeta xmlns:x=\"adobe:ns:meta/\">"
        + "<rdf:RDF xmlns:rdf=\"http://www.w3.org/1999/02/22-rdf-syntax-ns#\"><rdf:Description rdf:about=\"\" xmlns:dc=\"http://purl.org/dc/elements/1.1/\">"
        + "<dc:title><rdf:Alt><rdf:li xml:lang=\"x-default\">Old title</rdf:li></rdf:Alt></dc:title></rdf:Description></rdf:RDF></x:xmpmeta><?xpacket end=\"w\"?>";

    /// <summary>A UTF-32 packet in either byte order, with or without a mark, takes the edit and keeps its encoding.</summary>
    /// <param name="bigEndian">Whether the packet is big-endian.</param>
    /// <param name="mark">Whether the packet starts with a byte order mark.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false, false)]
    [Arguments(false, true)]
    [Arguments(true, false)]
    [Arguments(true, true)]
    public async Task Utf32PacketIsEditedInItsOwnEncoding(bool bigEndian, bool mark)
    {
        var encoding = new UTF32Encoding(bigEndian, mark);
        byte[] original = [.. encoding.GetPreamble(), .. encoding.GetBytes(Packet)];
        await Assert.That(XmpTranscoder.Detect(original, out var markLength)).IsEqualTo(bigEndian ? XmpEncoding.Utf32BigEndian : XmpEncoding.Utf32LittleEndian);
        await Assert.That(markLength).IsEqualTo(mark ? encoding.GetPreamble().Length : 0);
        await Assert.That(XmpParser.Parse(original).Title).IsEqualTo("Old title");

        var edited = XmpEditor.Apply(original, [new XmpChange(XmpProperty.Title, NewTitle)]);
        await Assert.That(edited).IsNotNull();
        await Assert.That(edited!.AsSpan(0, markLength).SequenceEqual(original.AsSpan(0, markLength))).IsTrue();
        await Assert.That(XmpParser.Parse(edited).Title).IsEqualTo(NewTitle);
        await Assert.That(encoding.GetString(edited, markLength, edited.Length - markLength)).Contains(NewTitle);
    }

    /// <summary>A packet that is already right is reported as editable but unchanged, which a PDF/A edit accepts.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnchangedPacketIsEditable()
    {
        var edited = XmpEditor.TryApply(Encoding.UTF8.GetBytes(Packet), [new XmpChange(XmpProperty.Title, "Old title")], out var packet);
        await Assert.That(edited).IsTrue();
        await Assert.That(packet).IsNull();
    }

    /// <summary>A packet that is not well-formed cannot be edited.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BrokenPacketIsNotEditable()
    {
        var edited = XmpEditor.TryApply(Encoding.UTF8.GetBytes(Packet.Replace("</x:xmpmeta>", string.Empty, StringComparison.Ordinal)), [new XmpChange(XmpProperty.Title, NewTitle)], out var packet);
        await Assert.That(edited).IsFalse();
        await Assert.That(packet).IsNull();
    }
}
