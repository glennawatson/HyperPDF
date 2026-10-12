// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Visits form XObjects, annotation appearances and Type 3 glyphs, which are content streams of their own.</summary>
internal static class PdfAFormWalk
{
    /// <summary>Visits an XObject painted with <c>Do</c>: an image or a form.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="resource">The XObject resource.</param>
    /// <param name="frame">Where it is painted.</param>
    internal static void Paint(PdfAColourState state, in PdfAResource resource, in PdfAContentFrame frame)
    {
        if (StoreReading.Resolve(state.Store, resource.Raw).AsStream() is not { } stream)
        {
            return;
        }

        var subtype = stream.Dictionary.Get(KnownName.Subtype);
        if (subtype.IsName(KnownName.Image))
        {
            PdfAImageChecks.VisitImage(state, resource, frame);
        }
        else if (subtype.IsName(KnownName.Form))
        {
            VisitForm(state, resource.Raw, frame.Scope, frame.Paint);
        }
    }

    /// <summary>
    /// Visits a form. Its content reads its own resources and then the page's. An isolated transparency group sets the
    /// blending space while its content is read; the group's colour space is visited after.
    /// </summary>
    /// <param name="state">The walk.</param>
    /// <param name="raw">The form, possibly a reference.</param>
    /// <param name="parent">The resources of the content that paints it.</param>
    /// <param name="inherited">The graphics state it inherits, or null for an appearance stream, which starts afresh.</param>
    internal static void VisitForm(PdfAColourState state, PdfValue raw, in PdfAColourResourceScope parent, PdfAColourPaintState? inherited)
    {
        if (StoreReading.Resolve(state.Store, raw).AsStream() is not { } form || state.FormDepth >= PdfLimits.MaxNesting)
        {
            return;
        }

        state.FormDepth++;
        try
        {
            var scope = PdfAResources.Extend(state, parent, form.Dictionary);
            var group = form.Dictionary.GetDictionary(KnownName.Group);
            var blending = PdfATransparency.FormBlendingSpace(state, group);
            var outer = state.CurrentBlendingSpace;
            if (blending is not null)
            {
                PdfATransparency.Enter(state, blending);
            }

            PdfAContentWalk.WalkStream(state, raw, scope, inherited ?? PdfAContentWalk.InitialState(state, scope));
            if (blending is not null)
            {
                PdfATransparency.Enter(state, outer);
            }

            PdfATransparency.VisitGroupSpace(state, form.Dictionary.GetRaw(KnownName.Group), scope.Own);
        }
        finally
        {
            state.FormDepth--;
        }
    }

    /// <summary>Visits the appearance streams of an annotation: every normal, down and rollover appearance.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="annotation">The annotation.</param>
    /// <param name="pageScope">The page's resources.</param>
    internal static void VisitAppearances(PdfAColourState state, PdfDictionary annotation, in PdfAColourResourceScope pageScope)
    {
        if (annotation.GetDictionary(KnownName.AP) is not { } appearances)
        {
            return;
        }

        VisitAppearance(state, appearances.GetRaw(KnownName.N), pageScope);
        VisitAppearance(state, appearances.GetRaw(KnownName.D), pageScope);
        VisitAppearance(state, appearances.GetRaw(KnownName.R), pageScope);
    }

    /// <summary>Visits a Type 3 font's glyph procedures once, when text is shown with it.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="font">The font resource.</param>
    /// <param name="frame">Where the text is shown.</param>
    internal static void VisitType3Font(PdfAColourState state, in PdfAResource font, in PdfAContentFrame frame)
    {
        if (StoreReading.Resolve(state.Store, font.Raw).AsDictionary() is not { } dictionary
            || !dictionary.IsName(KnownName.Subtype, KnownName.Type3)
            || (font.Id.IsValid && !state.Visited.Add(new(font.Id, PdfAVisitKind.Type3Font, 0))))
        {
            return;
        }

        var scope = PdfAResources.Extend(state, frame.Scope, dictionary);
        var procedures = dictionary.GetDictionary(KnownName.CharProcs);
        for (var i = 0; procedures is not null && i < procedures.Count; i++)
        {
            var raw = procedures.GetValueAt(i);
            if (StoreReading.Resolve(state.Store, raw).AsStream() is not { } glyph)
            {
                continue;
            }

            var glyphScope = glyph.Dictionary.ContainsKey(KnownName.Resources) ? PdfAResources.Extend(state, scope, glyph.Dictionary) : scope;
            PdfAContentWalk.WalkStream(state, raw, glyphScope, frame.Paint);
        }
    }

    /// <summary>Visits one appearance entry: a stream, or a dictionary of streams by state.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="raw">The entry.</param>
    /// <param name="pageScope">The page's resources.</param>
    private static void VisitAppearance(PdfAColourState state, PdfValue raw, in PdfAColourResourceScope pageScope)
    {
        var value = StoreReading.Resolve(state.Store, raw);
        if (value.AsStream() is not null)
        {
            VisitForm(state, raw, pageScope, null);
            return;
        }

        var states = value.AsDictionary();
        for (var i = 0; states is not null && i < states.Count; i++)
        {
            var entry = states.GetValueAt(i);
            if (StoreReading.Resolve(state.Store, entry).AsStream() is not null)
            {
                VisitForm(state, entry, pageScope, null);
            }
        }
    }
}
