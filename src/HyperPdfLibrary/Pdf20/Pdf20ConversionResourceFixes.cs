// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Pdf20;

/// <summary>
/// Removes deprecated entries that carry no content: the names of fonts, images and forms (ISO 32000-2 9.6.2.1, 8.9.5.1,
/// 8.10.2), font descriptor glyph lists (9.8.1, 9.8.3.1), procedure sets (14.2) and Web Capture identifiers (14.10.6).
/// </summary>
internal static class Pdf20ConversionResourceFixes
{
    /// <summary>Fixes one dictionary.</summary>
    /// <param name="tally">The counts.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="copy">The copy, made on the first edit.</param>
    internal static void Fix(Pdf20ConversionTally tally, PdfDictionary dictionary, ref PdfDictionary? copy)
    {
        var type = dictionary.Get(KnownName.Type).AsName().ToKnownName();
        var subtype = dictionary.Get(KnownName.Subtype).AsName().ToKnownName();
        switch (type)
        {
            case KnownName.Font:
                {
                    tally.ResourceNames += Pdf20ConversionEdit.Remove(dictionary, ref copy, KnownName.Name) ? 1 : 0;
                    break;
                }

            case KnownName.FontDescriptor:
                {
                    tally.GlyphSets += Pdf20ConversionEdit.Remove(dictionary, ref copy, KnownName.CharSet) ? 1 : 0;
                    tally.GlyphSets += Pdf20ConversionEdit.Remove(dictionary, ref copy, KnownName.CIDSet) ? 1 : 0;
                    break;
                }

            case KnownName.Page:
                {
                    tally.WebCaptureIds += Pdf20ConversionEdit.Remove(dictionary, ref copy, KnownName.ID) ? 1 : 0;
                    break;
                }

            default:
                {
                    break;
                }
        }

        tally.LegacyCollections += IsLegacyCollection(dictionary, tally.Names.Ordering) ? 1 : 0;
        FixXObject(tally, dictionary, subtype, ref copy);
        if (dictionary.Get(KnownName.ProcSet).Kind == PdfKind.Array)
        {
            tally.ProcSets += Pdf20ConversionEdit.Remove(dictionary, ref copy, KnownName.ProcSet) ? 1 : 0;
        }
    }

    /// <summary>Determines whether a CIDSystemInfo dictionary names Adobe-Korea1 or Adobe-Japan2, which PDF 2.0 deprecates (9.7.5.2).</summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="ordering">The /Ordering name.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsLegacyCollection(PdfDictionary dictionary, PdfName ordering) =>
        dictionary.GetText(ordering) is "Korea1" or "Japan2" && dictionary.GetText(KnownName.Registry) == "Adobe";

    /// <summary>Fixes an image or form XObject.</summary>
    /// <param name="tally">The counts.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="subtype">The dictionary's subtype.</param>
    /// <param name="copy">The copy, made on the first edit.</param>
    private static void FixXObject(Pdf20ConversionTally tally, PdfDictionary dictionary, KnownName subtype, ref PdfDictionary? copy)
    {
        if (subtype is not (KnownName.Image or KnownName.Form))
        {
            return;
        }

        tally.ResourceNames += Pdf20ConversionEdit.Remove(dictionary, ref copy, KnownName.Name) ? 1 : 0;
        if (subtype == KnownName.Image)
        {
            tally.WebCaptureIds += Pdf20ConversionEdit.Remove(dictionary, ref copy, KnownName.ID) ? 1 : 0;
        }
    }
}
