// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Handles transparency for PDF/A-1, which forbids it (ISO 19005-1 §6.4). A transparency group on a page or form that
/// draws nothing transparent is removed, as it changes nothing; soft masks, constant alpha below 1 and blend modes
/// other than Normal stop the conversion. Later parts allow transparency.
/// </summary>
internal static class PdfAConversionTransparency
{
    /// <summary>Applies the check to a PDF/A-1 conversion.</summary>
    /// <param name="session">The conversion.</param>
    internal static void Apply(PdfAConversionSession session)
    {
        if (session.Part != PdfAClaim.Part1)
        {
            return;
        }

        var found = new PdfAConversionTransparencyTally();
        var changed = PdfAConversionRewriter.Rewrite(session, found, static (s, tally, dictionary) => Fix(s, dictionary, tally));
        var breaks = found.Effects > 0 && session.Breaks(
            PdfARule.ExtGStateSoftMask,
            PdfARule.ExtGStateFillAlpha,
            PdfARule.ExtGStateStrokeAlpha,
            PdfARule.ExtGStateBlendModePdfA1,
            PdfARule.XObjectSoftMaskPdfA1,
            PdfARule.TransparencyGroupPdfA1);
        if (breaks)
        {
            session.Refuse(
                "The document uses transparency, such as see-through objects or soft masks, which PDF/A-1 does not allow. Choose PDF/A-2 or later.",
                PdfARule.ExtGStateSoftMask,
                PdfARule.ExtGStateFillAlpha,
                PdfARule.XObjectSoftMaskPdfA1,
                PdfARule.TransparencyGroupPdfA1);
        }
        else if (changed > 0)
        {
            session.Changed("Removed transparency group settings from pages and forms that draw nothing transparent.", PdfARule.TransparencyGroupPdfA1);
        }
    }

    /// <summary>Counts a dictionary's transparency and removes a transparency group.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="dictionary">The dictionary.</param>
    /// <param name="tally">The counts.</param>
    /// <returns>A copy without the group, or <see langword="null"/>.</returns>
    private static PdfDictionary? Fix(PdfAConversionSession session, PdfDictionary dictionary, PdfAConversionTransparencyTally tally)
    {
        tally.Effects += CountEffects(dictionary);
        if (StoreReading.Resolve(session.Store, dictionary.GetRaw(KnownName.Group)).AsDictionary() is { } group && group.IsName(KnownName.S, KnownName.Transparency))
        {
            var copy = dictionary.Clone();
            _ = copy.Remove(KnownName.Group);
            return copy;
        }

        return null;
    }

    /// <summary>
    /// Counts a dictionary's soft mask, constant alpha below 1 and blend mode other than Normal. An annotation's /CA is
    /// counted by the annotation fixes instead.
    /// </summary>
    /// <param name="dictionary">The dictionary.</param>
    /// <returns>The number of effects.</returns>
    private static int CountEffects(PdfDictionary dictionary)
    {
        var mask = dictionary.GetRaw(KnownName.SMask);
        var effects = !mask.IsNull && !mask.IsName(KnownName.NoneName) ? 1 : 0;
        var annotation = dictionary.ContainsKey(KnownName.Subtype) && dictionary.ContainsKey(KnownName.Rect);
        effects += !annotation && (IsBelowOne(dictionary.Get(KnownName.CA)) || IsBelowOne(dictionary.Get(KnownName.FillAlpha))) ? 1 : 0;
        effects += dictionary.ContainsKey(KnownName.BM) && !IsNormal(dictionary.Get(KnownName.BM)) ? 1 : 0;
        return effects;
    }

    /// <summary>Determines whether a value is a number below 1.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsBelowOne(PdfValue value) => value.IsNumber && value.AsNumber() < 1;

    /// <summary>Determines whether a /BM value is Normal or Compatible; an array is read by its first name.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsNormal(PdfValue value)
    {
        if (value.AsArray() is { } modes)
        {
            value = modes.Get(0);
        }

        return value.AsName().ToKnownName() is KnownName.Normal or KnownName.Compatible;
    }
}
