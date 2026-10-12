// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The PDF/A rules on the graphics objects content uses: graphics state parameter dictionaries (ISO 19005-1, 6.2.8 and
/// 6.4; ISO 19005-2, 6.2.5 and 6.2.10; ISO 19005-4, 6.2.5 and 6.2.9), XObjects (ISO 19005-1, 6.2.4 to 6.2.7; ISO
/// 19005-2, 6.2.9; ISO 19005-4, 6.2.8) and transparency groups (ISO 19005-1, 6.4).
/// </summary>
internal static class PdfAGraphicsObjects
{
    /// <summary>The PDF/A part based on PDF 2.0, which keeps blend mode arrays whole.</summary>
    internal const int Part4 = 4;

    /// <summary>The largest difference from 1 an alpha value may have and still count as 1.</summary>
    private const double AlphaTolerance = 0.000001;

    /// <summary>The value veraPDF gives an alpha that is not a number, so the check fails.</summary>
    private const double NotANumberAlpha = 2;

    /// <summary>The blend modes ISO 32000-1 defines.</summary>
    private static readonly FrozenSet<KnownName> BlendModes = new[]
    {
        KnownName.Normal,
        KnownName.Compatible,
        KnownName.Multiply,
        KnownName.Screen,
        KnownName.Overlay,
        KnownName.Darken,
        KnownName.Lighten,
        KnownName.ColorDodge,
        KnownName.ColorBurn,
        KnownName.HardLight,
        KnownName.SoftLight,
        KnownName.Difference,
        KnownName.Exclusion,
        KnownName.Hue,
        KnownName.Saturation,
        KnownName.Color,
        KnownName.Luminosity,
    }.ToFrozenSet();

    /// <summary>Checks a graphics state parameter dictionary named by a <c>gs</c> operator.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="state">The graphics state dictionary.</param>
    /// <param name="where">Where it is used.</param>
    internal static void CheckExtGState(PdfAGraphicsWalk walk, PdfDictionary state, string where)
    {
        if (!walk.FirstVisit(state))
        {
            return;
        }

        var context = walk.Context;
        var names = walk.Names;
        context.Require(IsOne(state, KnownName.CA), PdfARule.ExtGStateStrokeAlpha, where);
        context.Require(IsOne(state, names.FillAlpha), PdfARule.ExtGStateFillAlpha, where);
        context.Require(!state.ContainsKey(KnownName.BM) || state.GetName(KnownName.BM).ToKnownName() is KnownName.Normal or KnownName.Compatible, PdfARule.ExtGStateBlendModePdfA1, where);
        context.Require(!state.ContainsKey(names.HalftoneOrigin), PdfARule.ExtGStateHalftoneOrigin, where);
        context.Require(!state.ContainsKey(names.HalftonePhase), PdfARule.ExtGStateHalftonePhase, where);
        context.Require(!state.ContainsKey(KnownName.SMask) || state.Get(KnownName.SMask).IsName(KnownName.NoneName), PdfARule.ExtGStateSoftMask, where);
        context.Require(!state.ContainsKey(KnownName.TR), PdfARule.ExtGStateTransfer, where);
        context.Require(!state.ContainsKey(KnownName.TR2) || state.GetName(KnownName.TR2) == names.Default, PdfARule.ExtGStateTransfer2, where);
        CheckBlendMode(walk, StateBlendMode(walk, state.Get(KnownName.BM)), where);
    }

    /// <summary>Checks a blend mode name against the modes ISO 32000-1 defines.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="mode">The blend mode, or null when there is none to check.</param>
    /// <param name="where">Where it is used.</param>
    internal static void CheckBlendMode(PdfAGraphicsWalk walk, PdfValue mode, string where)
    {
        if (!mode.TryGetName(out var name))
        {
            return;
        }

        walk.Context.Require(BlendModes.Contains(name.ToKnownName()), PdfARule.BlendModeName, where);
    }

    /// <summary>Checks the dictionary keys of an XObject: the rules every image, form and PostScript XObject follows.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="xobject">The XObject dictionary.</param>
    /// <param name="where">Where it is used.</param>
    internal static void CheckXObject(PdfAGraphicsWalk walk, PdfDictionary xobject, string where)
    {
        var context = walk.Context;
        context.Require(!xobject.IsName(KnownName.Subtype, KnownName.PS), PdfARule.XObjectPostScript, where);
        context.Require(!xobject.ContainsKey(KnownName.OPI), PdfARule.XObjectOpiPdfA1, where);
        context.Require(!xobject.ContainsKey(KnownName.SMask), PdfARule.XObjectSoftMaskPdfA1, where);
    }

    /// <summary>Checks a form XObject or appearance stream dictionary, then its transparency group.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="form">The form dictionary.</param>
    /// <param name="where">Where it is used.</param>
    internal static void CheckForm(PdfAGraphicsWalk walk, PdfDictionary form, string where)
    {
        if (!walk.FirstVisit(form))
        {
            return;
        }

        var context = walk.Context;
        CheckXObject(walk, form, where);
        var postScript = form.GetName(walk.Names.Subtype2).ToKnownName() == KnownName.PS || form.ContainsKey(KnownName.PS);
        var opi = form.ContainsKey(KnownName.OPI);
        context.Require(!postScript, PdfARule.FormPostScriptPdfA1, where);
        context.Require(!postScript && !opi, PdfARule.FormPostScript, where);
        context.Require(!opi, PdfARule.FormOpi, where);
        context.Require(!form.ContainsKey(KnownName.Ref), PdfARule.FormReference, where);
        CheckGroup(walk, form.Get(KnownName.Group), where);
    }

    /// <summary>Checks a page or form transparency group: PDF/A-1 does not allow transparency groups.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="group">The resolved <c>/Group</c> value.</param>
    /// <param name="where">Where it is used.</param>
    internal static void CheckGroup(PdfAGraphicsWalk walk, PdfValue group, string where)
    {
        if (group.Kind == PdfKind.Dictionary)
        {
            walk.Context.Require(!group.AsDictionary()!.IsName(KnownName.S, KnownName.Transparency), PdfARule.TransparencyGroupPdfA1, where);
        }
    }

    /// <summary>Gets the blend mode veraPDF checks in a graphics state: the name, or before PDF 2.0 the first name of an array.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="mode">The resolved <c>/BM</c> value.</param>
    /// <returns>The blend mode name, or null.</returns>
    private static PdfValue StateBlendMode(PdfAGraphicsWalk walk, PdfValue mode)
    {
        if (mode.AsArray() is not { } modes || walk.Context.Part == Part4)
        {
            return mode;
        }

        for (var i = 0; i < modes.Count; i++)
        {
            var item = modes.Get(i);
            if (item.TryGetName(out _))
            {
                return item;
            }
        }

        return default;
    }

    /// <summary>Determines whether an alpha entry is absent or 1.</summary>
    /// <param name="state">The graphics state dictionary.</param>
    /// <param name="key">The alpha key.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsOne(PdfDictionary state, PdfName key)
    {
        if (!state.ContainsKey(key))
        {
            return true;
        }

        var value = state.Get(key);
        var alpha = value.IsNumber ? value.AsNumber() : NotANumberAlpha;
        return Math.Abs(alpha - 1) < AlphaTolerance;
    }
}
