// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Content;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Reads content streams for the PDF/A colour and image rules, following the graphics state as veraPDF does: colour
/// operators, painting operators and text use colour spaces, and <c>Do</c>, <c>sh</c>, <c>BI</c> and <c>gs</c> reach
/// images, forms, shadings and halftones. Each content stream is read once.
/// </summary>
internal static class PdfAContentWalk
{
    /// <summary>How many operators are read between cancellation checks.</summary>
    private const int CheckInterval = 256;

    /// <summary>The highest text rendering mode.</summary>
    private const int MaxRenderMode = 7;

    /// <summary>Gets the graphics state a content stream starts with when it inherits none: DeviceGray, or DefaultGray when set.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="scope">The stream's resources.</param>
    /// <returns>The state.</returns>
    internal static PdfAColourPaintState InitialState(PdfAColourState state, in PdfAColourResourceScope scope)
    {
        var gray = PdfAResources.ColourSpace(state, scope, KnownName.DeviceGray);
        return new(gray, gray, null, null, 0, false, false, 0, true, default);
    }

    /// <summary>Reads a content stream once.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="raw">The stream, possibly a reference.</param>
    /// <param name="scope">The resources it reads.</param>
    /// <param name="initial">The graphics state it starts with.</param>
    internal static void WalkStream(PdfAColourState state, PdfValue raw, in PdfAColourResourceScope scope, in PdfAColourPaintState initial)
    {
        if (StoreReading.Resolve(state.Store, raw).AsStream() is not { } stream
            || (raw.IsReference && !state.Visited.Add(new(raw.AsReference(), PdfAVisitKind.ContentStream, 0))))
        {
            return;
        }

        var buffer = default(PooledBuffer);
        try
        {
            if (PdfAStreams.TryDecode(stream, ref buffer, state.Context.CancellationToken))
            {
                Walk(state, buffer.WrittenSpan, new(scope, initial));
            }
        }
        finally
        {
            buffer.Dispose();
        }
    }

    /// <summary>Reads a page's content, which may be split over several streams.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="contents">The <c>/Contents</c> value.</param>
    /// <param name="scope">The page's resources.</param>
    internal static void WalkPage(PdfAColourState state, PdfValue contents, in PdfAColourResourceScope scope)
    {
        var value = StoreReading.Resolve(state.Store, contents);
        if (value.AsStream() is not null)
        {
            WalkStream(state, contents, scope, InitialState(state, scope));
            return;
        }

        if (value.AsArray() is not { Count: > 0 } parts)
        {
            return;
        }

        var buffer = default(PooledBuffer);
        var part = default(PooledBuffer);
        try
        {
            for (var i = 0; i < parts.Count; i++)
            {
                part.Length = 0;
                if (parts.Get(i).AsStream() is not { } stream || !PdfAStreams.TryDecode(stream, ref part, state.Context.CancellationToken))
                {
                    continue;
                }

                buffer.Write(part.WrittenSpan);
                buffer.WriteByte((byte)'\n');
            }

            Walk(state, buffer.WrittenSpan, new(scope, InitialState(state, scope)));
        }
        finally
        {
            part.Dispose();
            buffer.Dispose();
        }
    }

    /// <summary>Reads content operators.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="content">The decoded content.</param>
    /// <param name="run">The stream's state.</param>
    private static void Walk(PdfAColourState state, ReadOnlySpan<byte> content, PdfAContentRun run)
    {
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader(content, state.Store.Names, operands);
        while (reader.Next(out var op))
        {
            state.OperatorsSinceCheck++;
            if (state.OperatorsSinceCheck >= CheckInterval)
            {
                state.OperatorsSinceCheck = 0;
                state.Context.CancellationToken.ThrowIfCancellationRequested();
            }

            var group = PdfAOperators.Group(op);
            if (group != PdfAOperatorGroup.None)
            {
                Dispatch(state, run, ref reader, group);
            }
        }
    }

    /// <summary>Applies one operator.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="run">The stream's state.</param>
    /// <param name="reader">The reader, positioned after the operator.</param>
    /// <param name="group">The operator's group.</param>
    private static void Dispatch(PdfAColourState state, PdfAContentRun run, ref ContentReader reader, PdfAOperatorGroup group)
    {
        if (group <= PdfAOperatorGroup.ShapeGlyph)
        {
            ApplyState(state, run, ref reader, group);
        }
        else if (group <= PdfAOperatorGroup.StrokePattern)
        {
            if (run.Paint.ProcessColour)
            {
                PdfAColourOperators.Apply(state, run, LastName(ref reader), group);
            }
        }
        else
        {
            ApplyPaint(state, run, ref reader, group);
        }
    }

    /// <summary>Applies an operator that changes the graphics state without using colour.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="run">The stream's state.</param>
    /// <param name="reader">The reader.</param>
    /// <param name="group">The operator's group.</param>
    private static void ApplyState(PdfAColourState state, PdfAContentRun run, ref ContentReader reader, PdfAOperatorGroup group)
    {
        switch (group)
        {
            case PdfAOperatorGroup.Save:
            {
                run.Saved.Push(run.Paint);
                break;
            }

            case PdfAOperatorGroup.Restore:
            {
                run.Paint = run.Saved.Count > 0 ? run.Saved.Pop() : run.Paint;
                break;
            }

            case PdfAOperatorGroup.GraphicsState:
            {
                var resource = PdfAResources.Find(state, run.Scope, KnownName.ExtGState, LastName(ref reader));
                run.Paint = PdfAGraphicsStateChecks.Apply(state, resource, run.Paint);
                break;
            }

            case PdfAOperatorGroup.Intent:
            {
                CheckIntent(state, run, LastName(ref reader));
                break;
            }

            default:
            {
                ApplyText(state, run, ref reader, group);
                break;
            }
        }
    }

    /// <summary>Applies <c>Tr</c>, <c>Tf</c> and <c>d1</c>.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="run">The stream's state.</param>
    /// <param name="reader">The reader.</param>
    /// <param name="group">The operator's group.</param>
    private static void ApplyText(PdfAColourState state, PdfAContentRun run, ref ContentReader reader, PdfAOperatorGroup group)
    {
        switch (group)
        {
            case PdfAOperatorGroup.RenderMode:
            {
                var mode = reader.Operand(0);
                var value = mode.Kind == ContentOperandKind.Number && float.IsInteger(mode.Number) ? (int)mode.Number : 0;
                run.Paint = run.Paint with { RenderMode = value is >= 0 and <= MaxRenderMode ? value : 0 };
                break;
            }

            case PdfAOperatorGroup.Font:
            {
                var name = reader.Operand(0) is { Kind: ContentOperandKind.Name } operand ? operand.Name : default;
                run.Paint = run.Paint with { Font = name.IsNone ? default : PdfAResources.Find(state, run.Scope, KnownName.Font, name) };
                break;
            }

            default:
            {
                run.Paint = run.Paint with { ProcessColour = false };
                break;
            }
        }
    }

    /// <summary>Checks <c>ri</c>, which veraPDF reads only where colour operators apply.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="run">The stream's state.</param>
    /// <param name="name">The intent name, or none.</param>
    private static void CheckIntent(PdfAColourState state, PdfAContentRun run, PdfName name)
    {
        if (run.Paint.ProcessColour && !name.IsNone)
        {
            PdfARenderingIntents.Check(state, name);
        }
    }

    /// <summary>Applies an operator that paints: paths, text, images, XObjects and shadings.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="run">The stream's state.</param>
    /// <param name="reader">The reader.</param>
    /// <param name="group">The operator's group.</param>
    private static void ApplyPaint(PdfAColourState state, PdfAContentRun run, ref ContentReader reader, PdfAOperatorGroup group)
    {
        switch (group)
        {
            case PdfAOperatorGroup.PaintFill or PdfAOperatorGroup.PaintStroke or PdfAOperatorGroup.PaintBoth:
            {
                PdfAColourOperators.Paint(state, run, group != PdfAOperatorGroup.PaintStroke, group != PdfAOperatorGroup.PaintFill);
                break;
            }

            case PdfAOperatorGroup.ShowText:
            {
                PdfAColourOperators.ShowText(state, run);
                break;
            }

            case PdfAOperatorGroup.InlineImage:
            {
                PdfAInlineImageChecks.Visit(state, reader.Content[reader.InlineImageDictionary], run.Frame);
                break;
            }

            case PdfAOperatorGroup.XObject:
            {
                PdfAFormWalk.Paint(state, PdfAResources.Find(state, run.Scope, KnownName.XObject, LastName(ref reader)), run.Frame);
                break;
            }

            default:
            {
                PdfAColourOperators.Shading(state, run, LastName(ref reader));
                break;
            }
        }
    }

    /// <summary>Gets the last operand when it is a name, as veraPDF reads resource names.</summary>
    /// <param name="reader">The reader.</param>
    /// <returns>The name, or none.</returns>
    private static PdfName LastName(ref ContentReader reader) =>
        reader.Operand(reader.OperandCount - 1) is { Kind: ContentOperandKind.Name } operand ? operand.Name : default;
}
