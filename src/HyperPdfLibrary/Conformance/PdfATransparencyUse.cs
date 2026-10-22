// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Finds whether a page draws with transparency, as the PDF/A blending colour space rules need: its content, the forms,
/// tiling patterns and Type 3 glyphs that content paints, and every appearance stream of its annotations. Forms, patterns,
/// glyphs and appearances are read from the default graphics state, so each is read once per page.
/// </summary>
internal static class PdfATransparencyUse
{
    /// <summary>The pattern type of a tiling pattern.</summary>
    private const int TilingPattern = 1;

    /// <summary>Determines whether a page draws with transparency.</summary>
    /// <param name="context">The validation.</param>
    /// <param name="page">The page.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    internal static bool PageUses(PdfAValidationContext context, PdfPage page)
    {
        var walk = new PdfATransparencyWalk(context.Document.Objects.Names, [with(ReferenceEqualityComparer.Instance)], context.CancellationToken);
        var content = default(PooledBuffer);
        try
        {
            context.Streams.AppendContents(page.Dictionary.Get(KnownName.Contents), ref content, context.CancellationToken);
            if (ContentUses(walk, content.WrittenSpan, page.Resources, 0))
            {
                return true;
            }
        }
        finally
        {
            content.Dispose();
        }

        return AnnotationsUse(walk, page);
    }

    /// <summary>Determines whether a content stream drawn as a unit, a form, pattern cell or Type 3 glyph, draws with transparency.</summary>
    /// <param name="walk">The search.</param>
    /// <param name="stream">The stream.</param>
    /// <param name="resources">The resources its content uses.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    internal static bool ProcedureUses(PdfATransparencyWalk walk, PdfStream stream, PdfDictionary? resources, int depth)
    {
        if (depth >= PdfLimits.MaxNesting || !walk.Visited.Add(stream))
        {
            return false;
        }

        walk.CancellationToken.ThrowIfCancellationRequested();
        return ContentUses(walk, stream.DecodeOrEmpty(), resources, depth + 1);
    }

    /// <summary>
    /// Determines whether painting an XObject is transparent: any XObject painted with a transparent fill state, an image
    /// with a soft mask, or a form that is a transparency group or draws with transparency.
    /// </summary>
    /// <param name="walk">The search.</param>
    /// <param name="state">The painting stream's graphics state.</param>
    /// <param name="name">The XObject's resource name.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    internal static bool XObjectUses(PdfATransparencyWalk walk, PdfAPaintState state, PdfName name)
    {
        if (Resource(state.Resources, KnownName.XObject, name).AsStream() is not { } xobject)
        {
            return false;
        }

        var dictionary = xobject.Dictionary;
        return dictionary.IsName(KnownName.Subtype, KnownName.Image)
            ? state.Current.FillsTransparent || dictionary.GetStream(KnownName.SMask) is not null || dictionary.GetInteger(KnownName.SMaskInData) > 0
            : dictionary.IsName(KnownName.Subtype, KnownName.Form) && (state.Current.FillsTransparent || FormUses(walk, xobject, state.Resources, state.Depth));
    }

    /// <summary>Determines whether a tiling pattern a path paints with draws its cell with transparency.</summary>
    /// <param name="walk">The search.</param>
    /// <param name="state">The painting stream's graphics state.</param>
    /// <param name="name">The pattern's resource name, or no name.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    internal static bool PatternUses(PdfATransparencyWalk walk, PdfAPaintState state, PdfName name) =>
        !name.IsNone
        && Resource(state.Resources, KnownName.Pattern, name).AsStream() is { } pattern
        && pattern.Dictionary.GetInt32(KnownName.PatternType) == TilingPattern
        && ProcedureUses(walk, pattern, pattern.Dictionary.GetDictionary(KnownName.Resources) ?? state.Resources, state.Depth);

    /// <summary>Gets a named resource of a category.</summary>
    /// <param name="resources">The resources.</param>
    /// <param name="category">The category, such as <c>/XObject</c>.</param>
    /// <param name="name">The resource name.</param>
    /// <returns>The resource, or the null value.</returns>
    internal static PdfValue Resource(PdfDictionary resources, KnownName category, PdfName name) =>
        resources.GetDictionary(category) is { } entries ? entries.Get(name) : default;

    /// <summary>Determines whether a page's annotations draw with transparency: a blend mode of their own, or any appearance stream.</summary>
    /// <param name="walk">The search.</param>
    /// <param name="page">The page.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    private static bool AnnotationsUse(PdfATransparencyWalk walk, PdfPage page)
    {
        for (var i = 0; page.Dictionary.GetArray(KnownName.Annots) is { } annotations && i < annotations.Count; i++)
        {
            if (annotations.GetDictionary(i) is not { } annotation)
            {
                continue;
            }

            // PDF 2.0 lets an annotation set its own blend mode.
            if (PdfAPaintOperators.IsBlended(annotation.Get(KnownName.BM))
                || (annotation.GetDictionary(KnownName.AP) is { } appearances && AppearancesUse(walk, appearances, page.Resources)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether any appearance stream, normal, rollover or down and of any state, draws with transparency.</summary>
    /// <param name="walk">The search.</param>
    /// <param name="appearances">The appearance dictionary.</param>
    /// <param name="resources">The page resources, used when a stream has none.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    private static bool AppearancesUse(PdfATransparencyWalk walk, PdfDictionary appearances, PdfDictionary? resources)
    {
        for (var i = 0; appearances.Owner is { } store && i < appearances.Count; i++)
        {
            var appearance = StoreReading.Resolve(store, appearances.GetValueAt(i));
            if (appearance.AsStream() is { } stream ? FormUses(walk, stream, resources, 0) : StatesUse(walk, appearance.AsDictionary(), resources))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether any stream of an appearance subdictionary draws with transparency.</summary>
    /// <param name="walk">The search.</param>
    /// <param name="states">The subdictionary, or <see langword="null"/>.</param>
    /// <param name="resources">The page resources, used when a stream has none.</param>
    /// <returns><see langword="true"/> when one does.</returns>
    private static bool StatesUse(PdfATransparencyWalk walk, PdfDictionary? states, PdfDictionary? resources)
    {
        for (var i = 0; states?.Owner is { } store && i < states.Count; i++)
        {
            if (StoreReading.Resolve(store, states.GetValueAt(i)).AsStream() is { } state && FormUses(walk, state, resources, 0))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// Determines whether a form XObject or appearance stream draws with transparency. A transparency group counts as
    /// transparency whatever it draws.
    /// </summary>
    /// <param name="walk">The search.</param>
    /// <param name="form">The stream.</param>
    /// <param name="inherited">The resources in force where it is painted, used when it has none.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool FormUses(PdfATransparencyWalk walk, PdfStream form, PdfDictionary? inherited, int depth) =>
        (form.Dictionary.GetDictionary(KnownName.Group) is { } group && group.IsName(KnownName.S, KnownName.Transparency))
        || ProcedureUses(walk, form, form.Dictionary.GetDictionary(KnownName.Resources) ?? inherited, depth);

    /// <summary>Reads a content stream from the default graphics state for the operators that paint with transparency.</summary>
    /// <param name="walk">The search.</param>
    /// <param name="content">The decoded content.</param>
    /// <param name="resources">The resources in force.</param>
    /// <param name="depth">The nesting depth.</param>
    /// <returns><see langword="true"/> when it paints with transparency.</returns>
    private static bool ContentUses(PdfATransparencyWalk walk, ReadOnlySpan<byte> content, PdfDictionary? resources, int depth)
    {
        if (resources is null)
        {
            return false;
        }

        var state = new PdfAPaintState(resources, depth);
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader(content, walk.Names, operands);
        try
        {
            while (reader.Next(out var op))
            {
                if (PdfAPaintOperators.Uses(walk, state, op, ref reader))
                {
                    return true;
                }
            }
        }
        catch (Exception e) when (e is InvalidDataException or PdfException or FormatException or OverflowException)
        {
            // Damaged content stops the search at the damage, as a viewer stops drawing there.
            return false;
        }

        return false;
    }
}
