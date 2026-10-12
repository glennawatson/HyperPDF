// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Fonts.CMaps;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// A font's /ToUnicode as veraPDF reads it: a CMap named, or embedded with a /CMapName, starting "Identity-" maps each
/// code to the character with that number; an embedded CMap maps through its bf entries.
/// </summary>
[DebuggerDisplay("PdfAToUnicode: identity {_identity}")]
internal sealed class PdfAToUnicode
{
    /// <summary>The longest text one code is read to.</summary>
    private const int MaxText = 512;

    /// <summary>Whether codes map to themselves.</summary>
    private readonly bool _identity;

    /// <summary>The embedded map, or null.</summary>
    private readonly ToUnicodeMap? _map;

    /// <summary>Initializes a new instance of the <see cref="PdfAToUnicode"/> class.</summary>
    /// <param name="identity">Whether codes map to themselves.</param>
    /// <param name="map">The embedded map, or null.</param>
    private PdfAToUnicode(bool identity, ToUnicodeMap? map)
    {
        _identity = identity;
        _map = map;
    }

    /// <summary>Gets the map of a font without /ToUnicode.</summary>
    internal static PdfAToUnicode None { get; } = new(false, null);

    /// <summary>Reads a /ToUnicode value.</summary>
    /// <param name="store">The objects.</param>
    /// <param name="value">The resolved value.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The map.</returns>
    internal static PdfAToUnicode Read(PdfObjectStore store, PdfValue value, CancellationToken cancellationToken)
    {
        if (value.TryGetName(out var name))
        {
            return new(store.Names.GetString(name).StartsWith("Identity-", StringComparison.Ordinal), null);
        }

        if (value.AsStream() is not { } stream)
        {
            return None;
        }

        if (stream.Dictionary.Get(KnownName.CMapName).TryGetName(out var cmapName) && store.Names.GetString(cmapName).StartsWith("Identity-", StringComparison.Ordinal))
        {
            return new(true, null);
        }

        if (!PdfAFontStreams.TryDecode(stream, cancellationToken, out var data))
        {
            return None;
        }

        try
        {
            return new(false, ToUnicodeMap.Parse(data));
        }
        catch (Exception ex) when (PdfAFontStreams.IsReadFailure(ex))
        {
            return None;
        }
    }

    /// <summary>Maps a code to text.</summary>
    /// <param name="code">The code.</param>
    /// <returns>The text, or null when the code is not mapped.</returns>
    internal string? Text(int code)
    {
        if (_identity)
        {
            return ((char)code).ToString();
        }

        if (_map is null)
        {
            return null;
        }

        Span<char> text = stackalloc char[MaxText];
        return _map.TryGetUnicode(code, text, out var written) ? new string(text[..written]) : null;
    }
}
