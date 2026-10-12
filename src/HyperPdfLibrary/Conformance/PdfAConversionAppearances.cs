// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Makes annotation appearance dictionaries meet PDF/A: only the normal appearance, as a stream, or for buttons as a
/// dictionary of states (ISO 19005-1 §6.5.3; ISO 19005-2 and -3 §6.3.3; ISO 19005-4:2020 §6.3.3).
/// </summary>
internal static class PdfAConversionAppearances
{
    /// <summary>The most /Parent links followed to find a field's type.</summary>
    private const int MaxParents = 32;

    /// <summary>Fixes an annotation's appearance.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="annotation">The annotation as read.</param>
    /// <param name="copy">The copy being fixed.</param>
    /// <param name="subtype">The annotation type.</param>
    /// <param name="zeroSize">Whether the annotation has no area.</param>
    /// <param name="tally">The counts.</param>
    internal static void Fix(PdfAConversionSession session, PdfDictionary annotation, PdfDictionary copy, string subtype, bool zeroSize, PdfAConversionAnnotationTally tally)
    {
        var store = session.Store;
        if (StoreReading.Resolve(store, annotation.GetRaw(KnownName.AP)).AsDictionary() is not { } appearance)
        {
            if (NeedsAppearance(session, subtype, zeroSize))
            {
                tally.MissingAppearances++;
            }

            return;
        }

        var normal = appearance.GetRaw(KnownName.N);
        if (normal.IsNull)
        {
            tally.MissingAppearances += NeedsAppearance(session, subtype, zeroSize) ? 1 : 0;
            _ = copy.Remove(KnownName.AP);
            tally.ExtraAppearances++;
            return;
        }

        var button = subtype == "Widget" && IsButton(store, annotation);
        var fixedNormal = button ? FixButton(store, normal, annotation, copy, tally) : FixSingle(store, normal, annotation, tally);
        if (appearance.Count == 1 && IsSame(fixedNormal, normal))
        {
            return;
        }

        var only = new PdfDictionary(store, 1);
        only.Set(KnownName.N, fixedNormal);
        copy.Set(KnownName.AP, PdfValue.FromDictionary(only));
        tally.ExtraAppearances += appearance.Count != 1 ? 1 : 0;
    }

    /// <summary>Records the appearance changes, or the annotations that stop the conversion.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="tally">The counts.</param>
    internal static void Report(PdfAConversionSession session, PdfAConversionAnnotationTally tally)
    {
        if (tally.MissingAppearances > 0)
        {
            session.Refuse(
                "Some annotations have no saved appearance, so they would look different in each reader.",
                PdfARule.AnnotationAppearancePdfA2,
                PdfARule.AnnotationAppearancePdfA4,
                PdfARule.WidgetAppearance);
        }

        if (tally.ExtraAppearances > 0)
        {
            session.Changed("Kept only the normal appearance of annotations, removing their rollover and pressed looks.", PdfARule.AnnotationOnlyNormalAppearance);
        }

        if (tally.StateAppearances > 0)
        {
            session.Changed("Kept the appearance each annotation shows and removed its other states.", PdfARule.AnnotationNormalAppearanceStream);
        }
    }

    /// <summary>Determines whether two values are the same reference or the same direct object.</summary>
    /// <param name="left">One value.</param>
    /// <param name="right">The other.</param>
    /// <returns><see langword="true"/> when they are.</returns>
    private static bool IsSame(PdfValue left, PdfValue right) =>
        left.Kind == right.Kind && (left.IsReference ? left.AsReference() == right.AsReference() : ReferenceEquals(left.AsDictionary(), right.AsDictionary()));

    /// <summary>Determines whether the level needs an appearance for an annotation.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="subtype">The annotation type.</param>
    /// <param name="zeroSize">Whether it has no area.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool NeedsAppearance(PdfAConversionSession session, string subtype, bool zeroSize) => session.Part == PdfAClaim.Part1
        ? subtype == "Widget"
        : !zeroSize && subtype is not ("Popup" or "Link") && !(subtype == "Projection" && session.Part == PdfAClaim.Part4);

    /// <summary>Determines whether a widget belongs to a button field, reading /FT through its parents.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="widget">The widget.</param>
    /// <returns><see langword="true"/> for a button.</returns>
    private static bool IsButton(PdfObjectStore store, PdfDictionary widget)
    {
        var node = widget;
        for (var i = 0; node is not null && i < MaxParents; i++)
        {
            if (node.ContainsKey(KnownName.FT))
            {
                return node.IsName(KnownName.FT, KnownName.Btn);
            }

            node = StoreReading.Resolve(store, node.GetRaw(KnownName.Parent)).AsDictionary();
        }

        return false;
    }

    /// <summary>Makes a non-button normal appearance one stream: the state the annotation shows.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="normal">The /N value.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="tally">The counts.</param>
    /// <returns>The /N value to keep.</returns>
    private static PdfValue FixSingle(PdfObjectStore store, PdfValue normal, PdfDictionary annotation, PdfAConversionAnnotationTally tally)
    {
        var resolved = StoreReading.Resolve(store, normal);
        if (resolved.AsStream() is not null || resolved.AsDictionary() is not { } states)
        {
            return normal;
        }

        var chosen = annotation.GetName(KnownName.AS);
        if (!chosen.IsNone && states.GetRaw(chosen) is { IsNull: false } shown && StoreReading.Resolve(store, shown).AsStream() is not null)
        {
            tally.StateAppearances++;
            return shown;
        }

        if (states.Count == 1 && StoreReading.Resolve(store, states.GetValueAt(0)).AsStream() is not null)
        {
            tally.StateAppearances++;
            return states.GetValueAt(0);
        }

        tally.MissingAppearances++;
        return normal;
    }

    /// <summary>Makes a button's normal appearance a dictionary of states; a single stream becomes the shown state.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="normal">The /N value.</param>
    /// <param name="annotation">The widget.</param>
    /// <param name="copy">The widget copy, which gets /AS when a state is made.</param>
    /// <param name="tally">The counts.</param>
    /// <returns>The /N value to keep.</returns>
    private static PdfValue FixButton(PdfObjectStore store, PdfValue normal, PdfDictionary annotation, PdfDictionary copy, PdfAConversionAnnotationTally tally)
    {
        var resolved = StoreReading.Resolve(store, normal);
        if (resolved.AsStream() is null)
        {
            if (resolved.AsDictionary() is not { Count: > 0 })
            {
                tally.MissingAppearances++;
            }

            return normal;
        }

        var state = annotation.GetName(KnownName.AS);
        if (state.IsNone)
        {
            state = KnownName.Yes;
            copy.Set(KnownName.AS, PdfValue.FromName(state));
        }

        var states = new PdfDictionary(store, 1);
        states.Set(state, normal);
        tally.StateAppearances++;
        return PdfValue.FromDictionary(states);
    }
}
