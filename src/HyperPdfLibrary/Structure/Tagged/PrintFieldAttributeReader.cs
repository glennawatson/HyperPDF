// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Structure.Tagged;

/// <summary>Reads non-interactive field attributes without interning missing keys or inheriting parent values.</summary>
internal static class PrintFieldAttributeReader
{
    /// <summary>Checks cancellation once per batch of 64 dictionary entries.</summary>
    private const int CancellationCheckMask = 63;

    /// <summary>Fills absent values from one eligible attribute object, preserving earlier precedence decisions.</summary>
    /// <param name="attributes">The attribute dictionary.</param>
    /// <param name="names">The document's name table.</param>
    /// <param name="current">The values selected from higher-priority objects.</param>
    /// <param name="cancellationToken">Cancels attribute lookup.</param>
    /// <returns>The retained names and decoded description.</returns>
    internal static PdfPrintFieldAttributes Read(PdfDictionary attributes, PdfNameTable names, in PdfPrintFieldAttributes current, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return new(
            current.Role ?? ReadName(attributes, names, "Role"u8, cancellationToken),
            current.Checked ?? ReadChecked(attributes, names, cancellationToken),
            current.Description ?? attributes.GetText(KnownName.Desc));
    }

    /// <summary>Prefers the canonical PDF 2.0 spelling and recovers the legacy key when its value is absent or damaged.</summary>
    /// <param name="attributes">The eligible attribute object.</param>
    /// <param name="names">The document's name table.</param>
    /// <param name="cancellationToken">Cancels attribute lookup.</param>
    /// <returns>The checked-state name, or null.</returns>
    private static string? ReadChecked(PdfDictionary attributes, PdfNameTable names, CancellationToken cancellationToken) =>
        ReadName(attributes, names, "Checked"u8, cancellationToken) ?? ReadName(attributes, names, "checked"u8, cancellationToken);

    /// <summary>Reads a name entry without adding an absent key to the document's name table.</summary>
    /// <param name="attributes">The attribute object.</param>
    /// <param name="names">The document's name table.</param>
    /// <param name="spelling">The UTF-8 entry key.</param>
    /// <param name="cancellationToken">Cancels attribute lookup.</param>
    /// <returns>The source name, including private values, or null.</returns>
    private static string? ReadName(PdfDictionary attributes, PdfNameTable names, ReadOnlySpan<byte> spelling, CancellationToken cancellationToken)
    {
        for (var index = 0; index < attributes.Count; index++)
        {
            if ((index & CancellationCheckMask) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            var key = attributes.GetKeyAt(index);
            if (names.NameEquals(key, spelling))
            {
                return attributes.Get(key).TryGetName(out var value) ? DecodeName(names, value) : null;
            }
        }

        return null;
    }

    /// <summary>Reuses fixed standard spellings and decodes private names only when a snapshot needs them.</summary>
    /// <param name="names">The document's name table.</param>
    /// <param name="name">The source name.</param>
    /// <returns>The standard spelling or the decoded private name.</returns>
    private static string DecodeName(PdfNameTable names, PdfName name) => names.GetSpelling(name) switch
    {
        [(byte)'r', (byte)'b'] => "rb",
        [(byte)'c', (byte)'b'] => "cb",
        [(byte)'p', (byte)'b'] => "pb",
        [(byte)'t', (byte)'v'] => "tv",
        [(byte)'l', (byte)'b'] => "lb",
        [(byte)'o', (byte)'n'] => "on",
        [(byte)'o', (byte)'f', (byte)'f'] => "off",
        [(byte)'n', (byte)'e', (byte)'u', (byte)'t', (byte)'r', (byte)'a', (byte)'l'] => "neutral",
        _ => names.GetString(name),
    };
}
