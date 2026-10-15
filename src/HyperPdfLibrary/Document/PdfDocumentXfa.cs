// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Xfa;

namespace HyperPdfLibrary.Document;

/// <summary>Reads XFA form data.</summary>
public static class PdfDocumentXfa
{
    /// <summary>The packet name given to a single-stream XFA.</summary>
    private const string SingleStreamPacketName = "xdp";

    /// <summary>The number of array elements per packet: a name and a stream.</summary>
    private const int PacketPairLength = 2;

    /// <summary>Gets a value indicating whether the interactive form has XFA data.</summary>
    /// <param name="document">The document.</param>
    /// <returns>True when the document has XFA data.</returns>
    public static bool HasXfa(PdfDocument document) => document.Catalog.GetDictionary(KnownName.AcroForm)?.Get(KnownName.XFA) is { IsNull: false };

    /// <summary>Gets the XFA packets of the interactive form (<c>/AcroForm /XFA</c>). The packets are returned as decoded bytes and are not interpreted.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The packets, or <see langword="null"/> when the form has no XFA data. Read once until the next edit.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfXfa? GetXfa(PdfDocument document) => PdfReadSnapshots.Read(document, PdfReadKind.Xfa, PdfDocumentXfa.ReadXfa);

    /// <summary>Reads the XFA packets of the interactive form.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The packets, or <see langword="null"/> when the form has no XFA data.</returns>
    private static PdfXfa? ReadXfa(PdfDocument document)
    {
        var xfa = document.Catalog.GetDictionary(KnownName.AcroForm)?.Get(KnownName.XFA) ?? default;
        if (xfa.AsStream() is { } single)
        {
            return new(Array.AsReadOnly([new PdfXfaPacket(PdfDocumentXfa.SingleStreamPacketName, [.. single.DecodeOrEmpty()])]), true);
        }

        if (xfa.AsArray() is not { } array)
        {
            return null;
        }

        var packets = new List<PdfXfaPacket>(array.Count / PdfDocumentXfa.PacketPairLength);
        for (var i = 0; i + 1 < array.Count; i += PdfDocumentXfa.PacketPairLength)
        {
            if (array.Get(i).Kind == PdfKind.String && array.Get(i + 1).AsStream() is { } stream)
            {
                packets.Add(new(PdfText.Decode(array.Get(i).AsStringBytes()), [.. stream.DecodeOrEmpty()]));
            }
        }

        return new(packets.AsReadOnly(), false);
    }
}
