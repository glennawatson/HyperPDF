// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Checks the CMaps of composite fonts for PDF/A-1, which needs every CMap other than Identity-H and Identity-V embedded,
/// including one an embedded CMap builds on with /UseCMap (ISO 19005-1 §6.3.3.3). The library has no CMap file it may
/// embed in its place, so such a font stops the conversion.
/// </summary>
internal static class PdfAConversionCMaps
{
    /// <summary>Refuses a PDF/A-1 conversion whose composite fonts use a CMap that is not embedded.</summary>
    /// <param name="session">The conversion.</param>
    internal static void Check(PdfAConversionSession session)
    {
        if (session.Part != PdfAClaim.Part1)
        {
            return;
        }

        var store = session.Store;
        var graph = PdfObjectGraph.Collect(store, false, session.CancellationToken);
        for (var number = 1; number <= graph.Count; number++)
        {
            if (graph.GetValue(number).AsDictionary() is not { } font || !font.IsName(KnownName.Subtype, KnownName.Type0) || IsEmbeddedOrIdentity(store, font.GetRaw(KnownName.Encoding)))
            {
                continue;
            }

            session.Refuse("A font uses a character map that is not in the file, and PDF/A-1 needs it embedded. Choose PDF/A-2 or later.", PdfARule.CMapNamePdfA1);
            return;
        }
    }

    /// <summary>Determines whether a font's /Encoding is Identity-H or Identity-V, or an embedded CMap that uses no other CMap.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="encoding">The /Encoding value.</param>
    /// <returns><see langword="true"/> when PDF/A-1 accepts it.</returns>
    private static bool IsEmbeddedOrIdentity(PdfObjectStore store, PdfValue encoding)
    {
        var resolved = StoreReading.Resolve(store, encoding);
        if (resolved.AsStream() is { } cmap)
        {
            var used = StoreReading.Resolve(store, cmap.Dictionary.GetRaw(KnownName.UseCMap));
            return used.IsNull || used.AsStream() is not null || IsIdentity(used);
        }

        return IsIdentity(resolved);
    }

    /// <summary>Determines whether a value names Identity-H or Identity-V.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsIdentity(PdfValue value) => value.IsName(KnownName.IdentityH) || value.IsName(KnownName.IdentityV);
}
