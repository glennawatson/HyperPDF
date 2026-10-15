// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.IO;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Reads the content a PDF/A validator checks text in, as veraPDF does: each page's content, the normal, rollover and
/// down appearances of its annotations, the forms they paint, the tiling patterns they select and the glyph procedures
/// of the Type 3 fonts they show. It records which fonts are shown, with which rendering modes and character codes.
/// </summary>
internal static class PdfATextWalk
{
    /// <summary>The rendering mode that neither fills nor strokes.</summary>
    internal const int InvisibleMode = 3;

    /// <summary>The highest rendering mode.</summary>
    private const int MaxMode = 7;

    /// <summary>The tiling pattern type.</summary>
    private const int TilingPattern = 1;

    /// <summary>Walks every page of the document.</summary>
    /// <param name="context">The validation.</param>
    /// <returns>The fonts found.</returns>
    /// <exception cref="OperationCanceledException">The validation was cancelled.</exception>
    internal static PdfATextWalkState Run(PdfAValidationContext context)
    {
        var state = new PdfATextWalkState(context);
        var document = context.Document;
        for (var i = 0; i < document.PageCount; i++)
        {
            context.CancellationToken.ThrowIfCancellationRequested();
            var page = PdfDocumentPages.GetPage(document, i);
            state.PageResources = page.Resources;
            if (page.Resources is { } resources)
            {
                state.AddResources(resources);
            }

            ReadPageContents(state, page);
            WalkAnnotations(state, page.Dictionary.GetArray(KnownName.Annots));
        }

        return state;
    }

    /// <summary>Reads a form, appearance, pattern or glyph procedure once for each starting state.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="stream">The stream.</param>
    /// <param name="resources">The stream's own resources, or null.</param>
    /// <param name="graphics">The starting font and rendering mode.</param>
    internal static void WalkStream(PdfATextWalkState state, PdfStream stream, PdfDictionary? resources, PdfATextGraphics graphics)
    {
        if (resources is not null)
        {
            state.AddResources(resources);
        }

        if (state.Depth >= PdfLimits.MaxNesting || !state.Visited.Add(new(stream.Dictionary, state.PageResources, graphics)))
        {
            return;
        }

        if (!PdfAFontStreams.TryDecode(stream, state.Validation.CancellationToken, out var content))
        {
            return;
        }

        state.Depth++;
        try
        {
            Read(state, new(resources, graphics, StructParents(stream.Dictionary)), content);
        }
        finally
        {
            state.Depth--;
        }
    }

    /// <summary>Reads a page's content, joining an array of streams into one stream as the standard requires.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="page">The page.</param>
    private static void ReadPageContents(PdfATextWalkState state, PdfPage page)
    {
        var structParents = StructParents(page.Dictionary);

        // A check that shares decoded content serves the page's whole content; a damaged stream is read below, one part at a time.
        if (PdfPageContentScope.TryLease(state.Validation.Document, page, state.Validation.CancellationToken, out var shared))
        {
            using (shared)
            {
                Read(state, new(null, default, structParents), shared.Content);
            }

            return;
        }

        var contents = page.Dictionary.Get(KnownName.Contents);
        if (contents.AsStream() is { } single)
        {
            if (PdfAFontStreams.TryDecode(single, state.Validation.CancellationToken, out var data))
            {
                Read(state, new(null, default, structParents), data);
            }

            return;
        }

        if (contents.AsArray() is not { } parts)
        {
            return;
        }

        using var joined = new PooledWriteStream();
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts.Get(i).AsStream() is not { } part || !PdfAFontStreams.TryDecode(part, state.Validation.CancellationToken, out var data))
            {
                continue;
            }

            joined.Write(data);
            joined.WriteByte((byte)'\n');
        }

        Read(state, new(null, default, structParents), joined.WrittenSpan);
    }

    /// <summary>Reads a page's or stream's /StructParents key.</summary>
    /// <param name="dictionary">The page or stream dictionary.</param>
    /// <returns>The key, or -1.</returns>
    private static int StructParents(PdfDictionary dictionary) =>
        dictionary.Get(KnownName.StructParents) is { Kind: PdfKind.Integer } key ? (int)key.AsInteger() : -1;

    /// <summary>Walks the appearances of a page's annotations.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="annotations">The <c>/Annots</c> array, or null.</param>
    private static void WalkAnnotations(PdfATextWalkState state, PdfArray? annotations)
    {
        for (var i = 0; annotations is not null && i < annotations.Count; i++)
        {
            if (annotations.Get(i).Kind != PdfKind.Dictionary || annotations.GetDictionary(i)?.GetDictionary(KnownName.AP) is not { } appearances)
            {
                continue;
            }

            WalkAppearance(state, appearances.Get(KnownName.N));
            WalkAppearance(state, appearances.Get(KnownName.D));
            WalkAppearance(state, appearances.Get(KnownName.R));
        }
    }

    /// <summary>Walks one appearance entry: a stream, or a dictionary of streams by state.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="entry">The resolved entry.</param>
    private static void WalkAppearance(PdfATextWalkState state, PdfValue entry)
    {
        if (entry.AsStream() is { } stream)
        {
            WalkStream(state, stream, stream.Dictionary.GetDictionary(KnownName.Resources), default);
            return;
        }

        if (entry.Kind != PdfKind.Dictionary || entry.AsDictionary() is not { } states)
        {
            return;
        }

        for (var i = 0; i < states.Count; i++)
        {
            if (StoreReading.Resolve(state.Store, states.GetValueAt(i)).AsStream() is { } appearance)
            {
                WalkStream(state, appearance, appearance.Dictionary.GetDictionary(KnownName.Resources), default);
            }
        }
    }

    /// <summary>Reads the operators of decoded content.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="frame">The stream's state.</param>
    /// <param name="content">The decoded content.</param>
    private static void Read(PdfATextWalkState state, PdfATextFrame frame, ReadOnlySpan<byte> content)
    {
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader(content, state.Store.Names, operands);
        while (reader.Next(out var op))
        {
            if (!ApplyState(state, frame, ref reader, op) && !PdfATextShow.Apply(state, frame, ref reader, op))
            {
                ApplyResource(state, frame, ref reader, op);
            }
        }
    }

    /// <summary>Applies an operator that changes the font, rendering mode, saved state or marked content.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="frame">The stream's state.</param>
    /// <param name="reader">The reader, positioned after the operator.</param>
    /// <param name="op">The operator.</param>
    /// <returns><see langword="true"/> when the operator was one of these.</returns>
    private static bool ApplyState(PdfATextWalkState state, PdfATextFrame frame, ref ContentReader reader, ContentOperator op)
    {
        switch (op)
        {
            case ContentOperator.Save:
            {
                frame.Saved.Push(frame.Graphics);
                return true;
            }

            case ContentOperator.Restore:
            {
                frame.Graphics = frame.Saved.TryPop(out var saved) ? saved : frame.Graphics;
                return true;
            }

            case ContentOperator.SetFont:
            {
                var name = reader.Operand(0);
                frame.Graphics = frame.Graphics with { Font = name.Kind == ContentOperandKind.Name ? PdfAFontLookup.Find(state, frame, name.Name) : null };
                return true;
            }

            case ContentOperator.SetRenderMode:
            {
                frame.Graphics = frame.Graphics with { Mode = RenderingMode(reader.Operand(0)) };
                return true;
            }

            default:
            {
                return PdfAMarkedText.Apply(state, frame, ref reader, op);
            }
        }
    }

    /// <summary>Applies an operator that names a resource: <c>gs</c>, <c>Do</c>, <c>scn</c> and <c>SCN</c>.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="frame">The stream's state.</param>
    /// <param name="reader">The reader, positioned after the operator.</param>
    /// <param name="op">The operator.</param>
    private static void ApplyResource(PdfATextWalkState state, PdfATextFrame frame, ref ContentReader reader, ContentOperator op)
    {
        var last = reader.Operand(reader.OperandCount - 1);
        if (last.Kind != ContentOperandKind.Name)
        {
            return;
        }

        switch (op)
        {
            case ContentOperator.SetGraphicsState:
            {
                ApplyGraphicsState(state, frame, FindResource(state, frame, KnownName.ExtGState, last.Name));
                break;
            }

            case ContentOperator.PaintXObject:
            {
                PaintForm(state, frame, FindResource(state, frame, KnownName.XObject, last.Name));
                break;
            }

            case ContentOperator.SetFillColorN or ContentOperator.SetStrokeColorN:
            {
                SelectPattern(state, frame, FindResource(state, frame, KnownName.Pattern, last.Name));
                break;
            }

            default:
            {
                break;
            }
        }
    }

    /// <summary>Finds a named resource in the stream's own resources, then the page's.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="frame">The stream's state.</param>
    /// <param name="category">The resource category.</param>
    /// <param name="name">The resource name.</param>
    /// <returns>The resolved resource, or null.</returns>
    private static PdfValue FindResource(PdfATextWalkState state, PdfATextFrame frame, KnownName category, PdfName name)
    {
        var own = frame.Resources?.GetDictionary(category)?.Get(name) ?? default;
        return own.IsNull ? state.PageResources?.GetDictionary(category)?.Get(name) ?? default : own;
    }

    /// <summary>Applies the <c>/Font</c> entry of a graphics state parameter dictionary.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="frame">The stream's state.</param>
    /// <param name="parameters">The resolved dictionary.</param>
    private static void ApplyGraphicsState(PdfATextWalkState state, PdfATextFrame frame, PdfValue parameters)
    {
        if (parameters.AsDictionary()?.GetArray(KnownName.Font) is { Count: > 0 } font && PdfAFontLookup.FromValue(state, font.GetRaw(0)) is { } use)
        {
            frame.Graphics = frame.Graphics with { Font = use };
        }
    }

    /// <summary>Reads a form XObject painted by <c>Do</c>, starting from the current state.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="frame">The stream's state.</param>
    /// <param name="xobject">The resolved XObject.</param>
    private static void PaintForm(PdfATextWalkState state, PdfATextFrame frame, PdfValue xobject)
    {
        if (xobject.AsStream() is { } form && form.Dictionary.IsName(KnownName.Subtype, KnownName.Form))
        {
            WalkStream(state, form, form.Dictionary.GetDictionary(KnownName.Resources), frame.Graphics);
        }
    }

    /// <summary>Reads a tiling pattern selected as a colour, starting from the current state.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="frame">The stream's state.</param>
    /// <param name="pattern">The resolved pattern.</param>
    private static void SelectPattern(PdfATextWalkState state, PdfATextFrame frame, PdfValue pattern)
    {
        if (pattern.AsStream() is { } tiling && tiling.Dictionary.GetInt32(KnownName.PatternType) == TilingPattern)
        {
            WalkStream(state, tiling, tiling.Dictionary.GetDictionary(KnownName.Resources), frame.Graphics);
        }
    }

    /// <summary>Reads a <c>Tr</c> operand; anything but an integer from 0 to 7 means fill, as in veraPDF.</summary>
    /// <param name="operand">The operand.</param>
    /// <returns>The rendering mode.</returns>
    private static int RenderingMode(ContentOperand operand)
    {
        var value = operand.Number;
        return operand.Kind == ContentOperandKind.Number && value >= 0 && value <= MaxMode && float.IsInteger(value) ? (int)value : 0;
    }
}
