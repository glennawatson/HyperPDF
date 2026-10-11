// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Numerics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Graphics.Colors;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Content;

/// <summary>Runs the interpreter's Execution operations over its owned state.</summary>
internal static class ContentExecution
{
    /// <summary>The initial capacity of the state stack.</summary>
    internal const int StackCapacity = 16;

    /// <summary>The operators run between checks of the running call's cancellation token.</summary>
    internal const int CancelCheckOperators = 128;

    /// <summary>The newline written between the streams of a content array.</summary>
    internal const byte StreamSeparator = (byte)'\n';

    /// <summary>Decodes a page's /Contents, a stream or an array of streams, into one buffer.</summary>
    /// <param name = "page">The page.</param>
    /// <param name = "buffer">Receives the content.</param>
    /// <param name="cancellationToken">Cancels stream decoding.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void DecodeContents(PdfPage page, ref PooledBuffer buffer, CancellationToken cancellationToken = default) =>
        ContentExecution.AppendContents(page.Dictionary.Get(KnownName.Contents), ref buffer, cancellationToken);

    /// <summary>Decodes a /Contents value, a stream or an array of streams, into one buffer.</summary>
    /// <param name = "contents">The /Contents value.</param>
    /// <param name = "buffer">Receives the content.</param>
    /// <param name="cancellationToken">Cancels stream decoding.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static void DecodeContents(PdfValue contents, ref PooledBuffer buffer, CancellationToken cancellationToken = default) =>
        ContentExecution.AppendContents(contents, ref buffer, cancellationToken);

    /// <summary>Runs a page's content, then returns the device to its starting state.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "page">The page; content is drawn in viewer space.</param>
    internal static void RunPage(ContentInterpreter self, PdfPage page)
    {
        ContentExecution.BeginPage(self, page);
        var content = default(PooledBuffer);
        try
        {
            ContentExecution.DecodeContents(page, ref content, self.CancellationToken);
            ContentExecution.Execute(self, content.WrittenSpan);
        }
        finally
        {
            content.Dispose();
        }

        ContentExecution.EndPage(self);
    }

    /// <summary>Starts running a page's content in slices with <see cref = "ContentExecution.RunSlice"/>; content is drawn in viewer space.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "page">The page.</param>
    internal static void BeginPage(ContentInterpreter self, PdfPage page)
    {
        self.State = new() { Ctm = page.ViewerTransform, };
        self.PatternBase = page.ViewerTransform;
        self.Resources.Add(page.Resources);
    }

    /// <summary>
    /// Runs up to a number of top-level operators of the page's content, then stops at an operator boundary so the
    /// caller can pause. Forms, patterns and glyphs an operator draws always run to their end.
    /// </summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "content">The page's whole decoded content.</param>
    /// <param name = "position">The offset to resume at; receives the offset to resume from next time.</param>
    /// <param name = "operators">The most operators to run.</param>
    /// <returns><see langword="true"/> when the content has ended.</returns>
    internal static bool RunSlice(ContentInterpreter self, ReadOnlySpan<byte> content, ref int position, int operators)
    {
        self.CancellationToken.ThrowIfCancellationRequested();
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader(content, self.Cache.Names, operands) { Position = position, };
        for (var i = 0; i < operators; i++)
        {
            if (!reader.Next(out var op))
            {
                position = content.Length;
                return true;
            }

            ContentOperators.Dispatch(self, op, ref reader);
        }

        position = reader.Position;
        return false;
    }

    /// <summary>Ends a page run in slices, returning the device to its starting state.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    internal static void EndPage(ContentInterpreter self)
    {
        ContentExecution.Unwind(self);
        self.Resources.Clear();
    }

    /// <summary>Draws a form XObject with a transform, as annotation appearances are drawn.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "form">The form stream.</param>
    /// <param name = "ctm">The matrix from the form's space to the page.</param>
    /// <param name = "inherited">The resources used when the form has none of its own, or null.</param>
    internal static void RunForm(ContentInterpreter self, PdfStream form, Matrix3x2 ctm, PdfDictionary? inherited)
    {
        self.State = new() { Ctm = ctm, };
        self.PatternBase = ctm;
        self.Resources.Add(inherited);
        ContentXObjects.DrawForm(self, form);
        self.Resources.Clear();
    }

    /// <summary>Runs a tiling pattern's content in pattern space.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "pattern">The pattern stream.</param>
    /// <param name = "uncolored">Whether the pattern takes its colour from the colour that selected it.</param>
    /// <param name = "rgb">That colour as 0xRRGGBB.</param>
    internal static void RunPattern(ContentInterpreter self, PdfStream pattern, bool uncolored, uint rgb)
    {
        self.State = new() { Ctm = Matrix3x2.Identity, };
        self.PatternBase = Matrix3x2.Identity;
        if (uncolored)
        {
            var colour = new ColorState(PdfColorSpace.DeviceRgb, rgb, null, false);
            self.State.Fill = colour;
            self.State.Stroke = colour;
            self.ColorLocked = true;
        }

        self.Resources.Add(pattern.Dictionary.GetDictionary(KnownName.Resources));
        ContentExecution.RunStreamBody(self, pattern);
        ContentExecution.Unwind(self);
        self.Resources.Clear();
    }

    /// <summary>Determines whether an exception from a damaged object should only skip the operator that hit it.</summary>
    /// <param name = "exception">The exception.</param>
    /// <returns><see langword="true"/> when the exception is recoverable.</returns>
    internal static bool IsRecoverable(Exception exception) =>
        exception is InvalidDataException or
        PdfException or
        ArgumentException or
        InvalidOperationException or
        IndexOutOfRangeException or
        NotSupportedException or
        FormatException or
        OverflowException;

    /// <summary>Decodes a page's /Contents, a stream or an array of streams, into one buffer.</summary>
    /// <param name = "contents">The /Contents value.</param>
    /// <param name = "buffer">Receives the content.</param>
    /// <param name="cancellationToken">Cancels stream decoding.</param>
    internal static void AppendContents(PdfValue contents, ref PooledBuffer buffer, CancellationToken cancellationToken = default)
    {
        if (contents.AsStream() is { } single)
        {
            _ = single.Decode(ref buffer, cancellationToken);
            return;
        }

        if (contents.AsArray() is not { } array)
        {
            return;
        }

        for (var i = 0; i < array.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (array.Get(i).AsStream() is not { } part)
            {
                continue;
            }

            var piece = default(PooledBuffer);
            try
            {
                _ = part.Decode(ref piece, cancellationToken);
                buffer.Write(piece.WrittenSpan);
                buffer.WriteByte(ContentExecution.StreamSeparator);
            }
            finally
            {
                piece.Dispose();
            }
        }
    }

    /// <summary>Runs a stream's content with the current state and resources.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "stream">The stream.</param>
    internal static void RunStreamBody(ContentInterpreter self, PdfStream stream)
    {
        var content = default(PooledBuffer);
        try
        {
            _ = stream.Decode(ref content, self.CancellationToken);
            ContentExecution.Execute(self, content.WrittenSpan);
        }
        finally
        {
            content.Dispose();
        }
    }

    /// <summary>Runs content, dispatching each operator through the table.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "content">The decoded content.</param>
    internal static void Execute(ContentInterpreter self, ReadOnlySpan<byte> content)
    {
        var stackFloor = self.StackFloor;
        var markedFloor = self.MarkedFloor;
        self.StackFloor = self.Stack.Count;
        self.MarkedFloor = self.Marked.Count;
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader(content, self.Cache.Names, operands);
        var batch = 0;
        while (reader.Next(out var op))
        {
            ContentOperators.Dispatch(self, op, ref reader);
            batch = (batch + 1) % ContentExecution.CancelCheckOperators;
            if (batch == 0)
            {
                self.CancellationToken.ThrowIfCancellationRequested();
            }
        }

        ContentExecution.Unwind(self);
        self.StackFloor = stackFloor;
        self.MarkedFloor = markedFloor;
    }

    /// <summary>Restores every state saved by the current stream and closes its marked content.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    internal static void Unwind(ContentInterpreter self)
    {
        while (self.Stack.Count > self.StackFloor)
        {
            ContentGraphicsState.RestoreState(self);
        }

        while (self.Marked.Count > self.MarkedFloor)
        {
            ContentMarked.EndMarked(self);
        }

        self.PendingClip = false;
        self.Path.Reset();
        self.PathPoints = 0;
    }

    /// <summary>Gets the innermost resource dictionary.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <returns>The dictionary, or null.</returns>
    internal static PdfDictionary? CurrentResources(ContentInterpreter self) => self.Resources.Count == 0 ? null : self.Resources[^1];

    /// <summary>Finds a named resource, searching the innermost resources first.</summary>
    /// <param name = "self">The owned interpreter state.</param>
    /// <param name = "category">The resource category, such as /Font.</param>
    /// <param name = "name">The resource name.</param>
    /// <returns>The value, resolved; null when missing.</returns>
    internal static PdfValue FindResource(ContentInterpreter self, KnownName category, PdfName name)
    {
        for (var i = self.Resources.Count - 1; i >= 0; i--)
        {
            var value = self.Resources[i]?.GetDictionary(category)?.Get(name) ?? default;
            if (!value.IsNull)
            {
                return value;
            }
        }

        return default;
    }
}
