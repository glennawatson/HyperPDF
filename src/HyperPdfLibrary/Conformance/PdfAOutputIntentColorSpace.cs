// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Reads the colour space of the PDF/A output intent's destination profile, as the annotation and page rules use it.</summary>
internal static class PdfAOutputIntentColorSpace
{
    /// <summary>The offset of the colour space signature in an ICC profile header.</summary>
    private const int SignatureOffset = 16;

    /// <summary>The length of an ICC colour space signature.</summary>
    private const int SignatureLength = 4;

    /// <summary>Reads the colour space signature of the first PDF/A output intent with a destination profile.</summary>
    /// <param name="owner">The catalog, or a page with its own output intents.</param>
    /// <returns>The four-byte signature, such as "RGB ", or <see langword="null"/> when there is no such intent.</returns>
    internal static byte[]? Read(PdfDictionary owner)
    {
        if (owner.Array("OutputIntents") is not { } intents || owner.Owner is not { } store)
        {
            return null;
        }

        var pdfa = store.Names.Intern("GTS_PDFA1"u8);
        for (var i = 0; i < intents.Count; i++)
        {
            if (intents.GetDictionary(i) is not { } intent || intent.GetName(KnownName.S) != pdfa
                || intent.Stream("DestOutputProfile") is not { } profile)
            {
                continue;
            }

            var bytes = profile.DecodeOrEmpty();
            return bytes.Length >= SignatureOffset + SignatureLength ? bytes.AsSpan(SignatureOffset, SignatureLength).ToArray() : null;
        }

        return null;
    }
}
