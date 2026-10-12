// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Content;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Applies content operators for the PDF/A content rules: it tracks the graphics state the rules need, resolves the
/// resources operators name, and follows them into graphics states, XObjects, patterns and Type 3 glyphs.
/// </summary>
internal static class PdfAContentOperators
{
    /// <summary>The deepest <c>q</c> nesting PDF/A-1, PDF/A-2 and PDF/A-3 allow.</summary>
    private const int MaxSaveDepth = 28;

    /// <summary>The handler of each operator role, indexed by role.</summary>
    private static readonly Action<PdfAGraphicsWalk, PdfAContentState, PdfAOperands, PdfAOperatorTraits>[] Handlers =
    [
        static (_, _, _, _) => { },
        static (walk, state, _, _) => Save(walk, state),
        static (_, state, _, _) => Restore(state),
        SetFont,
        static (walk, state, _, _) => PdfAContentNested.ShowText(walk, state),
        static (_, state, _, _) => state.Graphics = state.Graphics with { ProcessColor = false },
        SetDeviceColor,
        SetColorSpace,
        SetPatternColor,
        static (walk, state, operands, _) => FindColored(walk, state, KnownName.Shading, operands.Last),
        SetGraphicsState,
        PaintXObject,
        static (walk, state, operands, _) => FindIfNamed(walk, state, KnownName.Properties, operands.Last),
        static (walk, state, _, _) => walk.Context.Fail(PdfARule.UndefinedOperator, state.Where),
    ];

    /// <summary>Applies one operator.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="state">The stream's state.</param>
    /// <param name="op">The operator.</param>
    /// <param name="operands">Its operands.</param>
    internal static void Apply(PdfAGraphicsWalk walk, PdfAContentState state, ContentOperator op, PdfAOperands operands)
    {
        var traits = PdfAOperatorTable.Get(op);
        Handlers[(int)traits.Role](walk, state, operands, traits);
    }

    /// <summary>Saves the graphics state and checks the nesting depth.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="state">The stream's state.</param>
    private static void Save(PdfAGraphicsWalk walk, PdfAContentState state)
    {
        state.Saved.Add(state.Graphics);
        walk.Context.Require(state.Saved.Count <= MaxSaveDepth, PdfARule.GraphicsStateNesting, state.Where);
    }

    /// <summary>Restores the state the last <c>q</c> saved; an unbalanced <c>Q</c> is ignored.</summary>
    /// <param name="state">The stream's state.</param>
    private static void Restore(PdfAContentState state)
    {
        if (state.Saved.Count == 0)
        {
            return;
        }

        state.Graphics = state.Saved[^1];
        state.Saved.RemoveAt(state.Saved.Count - 1);
    }

    /// <summary>Selects the font a <c>Tf</c> names.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="state">The stream's state.</param>
    /// <param name="operands">The operands.</param>
    /// <param name="traits">The operator's traits.</param>
    private static void SetFont(PdfAGraphicsWalk walk, PdfAContentState state, PdfAOperands operands, PdfAOperatorTraits traits)
    {
        var font = operands.First.IsNone ? default : PdfAResourceLookup.Find(walk, state, KnownName.Font, operands.First);
        state.Graphics = state.Graphics with { Font = font.Kind == PdfKind.Dictionary ? font.AsDictionary() : null };
    }

    /// <summary>Selects a device colour space, or the Default colour space that replaces it.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="state">The stream's state.</param>
    /// <param name="operands">The operands.</param>
    /// <param name="traits">The operator's traits.</param>
    private static void SetDeviceColor(PdfAGraphicsWalk walk, PdfAContentState state, PdfAOperands operands, PdfAOperatorTraits traits)
    {
        if (state.Graphics.ProcessColor)
        {
            SetPattern(state, traits.Stroke, PdfAResourceLookup.FindColorSpace(walk, state, traits.Device) == PdfAColorSpaceKind.Pattern);
        }
    }

    /// <summary>Selects the colour space a <c>cs</c> or <c>CS</c> names.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="state">The stream's state.</param>
    /// <param name="operands">The operands.</param>
    /// <param name="traits">The operator's traits.</param>
    private static void SetColorSpace(PdfAGraphicsWalk walk, PdfAContentState state, PdfAOperands operands, PdfAOperatorTraits traits)
    {
        if (!state.Graphics.ProcessColor)
        {
            return;
        }

        var kind = operands.Last.IsNone ? PdfAColorSpaceKind.Invalid : PdfAResourceLookup.FindColorSpace(walk, state, operands.Last);
        SetPattern(state, traits.Stroke, kind == PdfAColorSpaceKind.Pattern);
    }

    /// <summary>Follows an <c>scn</c> or <c>SCN</c> to the pattern it names when the colour space is a pattern space.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="state">The stream's state.</param>
    /// <param name="operands">The operands.</param>
    /// <param name="traits">The operator's traits.</param>
    private static void SetPatternColor(PdfAGraphicsWalk walk, PdfAContentState state, PdfAOperands operands, PdfAOperatorTraits traits)
    {
        var graphics = state.Graphics;
        if (!graphics.ProcessColor || !(traits.Stroke ? graphics.StrokePattern : graphics.FillPattern))
        {
            return;
        }

        SetPattern(state, traits.Stroke, PdfAContentNested.UsePattern(walk, state, operands.Last));
    }

    /// <summary>Checks the graphics state a <c>gs</c> names.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="state">The stream's state.</param>
    /// <param name="operands">The operands.</param>
    /// <param name="traits">The operator's traits.</param>
    private static void SetGraphicsState(PdfAGraphicsWalk walk, PdfAContentState state, PdfAOperands operands, PdfAOperatorTraits traits)
    {
        if (!operands.Last.IsNone && PdfAResourceLookup.Find(walk, state, KnownName.ExtGState, operands.Last).AsDictionary() is { } dictionary)
        {
            PdfAGraphicsObjects.CheckExtGState(walk, dictionary, state.Where);
        }
    }

    /// <summary>Follows a <c>Do</c> into the XObject it names.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="state">The stream's state.</param>
    /// <param name="operands">The operands.</param>
    /// <param name="traits">The operator's traits.</param>
    private static void PaintXObject(PdfAGraphicsWalk walk, PdfAContentState state, PdfAOperands operands, PdfAOperatorTraits traits)
    {
        if (operands.Last.IsNone)
        {
            return;
        }

        var xobject = PdfAResourceLookup.Find(walk, state, KnownName.XObject, operands.Last);
        if (!xobject.IsNull)
        {
            PdfAContentNested.UseXObject(walk, state, xobject);
        }
    }

    /// <summary>Looks up a named resource for an operator veraPDF reads only where colour is used.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="state">The stream's state.</param>
    /// <param name="category">The resource category.</param>
    /// <param name="name">The operand name, or none.</param>
    private static void FindColored(PdfAGraphicsWalk walk, PdfAContentState state, KnownName category, PdfName name)
    {
        if (state.Graphics.ProcessColor)
        {
            FindIfNamed(walk, state, category, name);
        }
    }

    /// <summary>Looks up a named resource when the operand is a name.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="state">The stream's state.</param>
    /// <param name="category">The resource category.</param>
    /// <param name="name">The operand name, or none.</param>
    private static void FindIfNamed(PdfAGraphicsWalk walk, PdfAContentState state, KnownName category, PdfName name)
    {
        if (!name.IsNone)
        {
            _ = PdfAResourceLookup.Find(walk, state, category, name);
        }
    }

    /// <summary>Records whether the fill or stroke colour space is now a pattern space.</summary>
    /// <param name="state">The stream's state.</param>
    /// <param name="stroke">Whether the stroke colour changed.</param>
    /// <param name="pattern">Whether it is a pattern space.</param>
    private static void SetPattern(PdfAContentState state, bool stroke, bool pattern) =>
        state.Graphics = stroke ? state.Graphics with { StrokePattern = pattern } : state.Graphics with { FillPattern = pattern };
}
