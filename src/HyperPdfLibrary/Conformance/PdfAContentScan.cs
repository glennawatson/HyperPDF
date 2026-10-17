// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Content;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Reads content streams for the PDF/A content rules: operators ISO 32000 does not define (ISO 19005-2, 6.2.2), graphics
/// state nesting (ISO 19005-2, 6.1.13) and the resources the operators name. Streams are read the way veraPDF reads them:
/// the parts of a <c>/Contents</c> array are joined with a carriage return, and a stream veraPDF cannot parse counts as
/// empty.
/// </summary>
internal static class PdfAContentScan
{
    /// <summary>How deeply forms, patterns and glyphs may nest before the walk stops reading further in.</summary>
    private const int MaxDepth = 100;

    /// <summary>How many operators are read between cancellation checks.</summary>
    private const int CancelInterval = 1024;

    /// <summary>The byte veraPDF puts between the parts of a <c>/Contents</c> array: a carriage return.</summary>
    private const byte PartSeparator = (byte)'\r';

    /// <summary>Recognises the filters decoded by the PDF/A content checker.</summary>
    /// <param name="name">The parsed filter name.</param>
    /// <returns>Whether the checker reads this filter's decoded operators.</returns>
    internal static bool IsReadableFilter(KnownName name) =>
        name is KnownName.FlateDecode or KnownName.ASCIIHexDecode or KnownName.ASCII85Decode
            or KnownName.LZWDecode or KnownName.RunLengthDecode or KnownName.Crypt;

    /// <summary>Reads a content stream, or an array of them, once.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="contents">The resolved content: a stream or an array of streams.</param>
    /// <param name="key">The object that identifies the content so it is checked once, or null to check it every time.</param>
    /// <param name="state">The stream's resources and starting graphics state.</param>
    internal static void Scan(PdfAGraphicsWalk walk, PdfValue contents, object? key, PdfAContentState state)
    {
        if ((key is not null && !walk.FirstVisit(key)) || walk.Depth >= MaxDepth)
        {
            return;
        }

        var cancellationToken = walk.Context.CancellationToken;
        cancellationToken.ThrowIfCancellationRequested();
        var buffer = default(PooledBuffer);
        walk.Depth++;
        try
        {
            if (TryDecode(contents, ref buffer, cancellationToken) && !HasUnparsableKeyword(walk, buffer.WrittenSpan))
            {
                Run(walk, state, buffer.WrittenSpan);
            }
        }
        finally
        {
            walk.Depth--;
            buffer.Dispose();
        }
    }

    /// <summary>Runs the operators of decoded content.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="state">The stream's state.</param>
    /// <param name="content">The decoded content.</param>
    private static void Run(PdfAGraphicsWalk walk, PdfAContentState state, ReadOnlySpan<byte> content)
    {
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader(content, walk.Store.Names, operands);
        var count = 0;
        while (reader.Next(out var op))
        {
            count++;
            if (count % CancelInterval == 0)
            {
                walk.Context.CancellationToken.ThrowIfCancellationRequested();
            }

            PdfAContentOperators.Apply(walk, state, op, Operands(ref reader));
        }
    }

    /// <summary>Gets the operand names the rules read.</summary>
    /// <param name="reader">The reader, positioned after an operator.</param>
    /// <returns>The first and last operands when they are names.</returns>
    private static PdfAOperands Operands(ref ContentReader reader)
    {
        var count = reader.OperandCount;
        var first = reader.Operand(0);
        var last = reader.Operand(count - 1);
        return new(
            first.Kind == ContentOperandKind.Name ? first.Name : default,
            last.Kind == ContentOperandKind.Name ? last.Name : default);
    }

    /// <summary>
    /// Determines whether the content has an operator veraPDF's parser rejects, which makes it read the whole stream as
    /// empty: a keyword starting with <c>I</c> other than <c>ID</c>.
    /// </summary>
    /// <param name="walk">The walk.</param>
    /// <param name="content">The decoded content.</param>
    /// <returns><see langword="true"/> when veraPDF would not parse the stream.</returns>
    private static bool HasUnparsableKeyword(PdfAGraphicsWalk walk, ReadOnlySpan<byte> content)
    {
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader(content, walk.Store.Names, operands);
        while (reader.Next(out var op))
        {
            if (op == ContentOperator.Unknown && content[OperatorStart(content, reader.Position)] == (byte)'I')
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Finds where the keyword that ends at an offset starts.</summary>
    /// <param name="content">The content.</param>
    /// <param name="end">The offset after the keyword.</param>
    /// <returns>The offset of its first byte.</returns>
    private static int OperatorStart(ReadOnlySpan<byte> content, int end)
    {
        var start = Math.Min(end, content.Length);
        while (start > 0 && !PdfCharacters.EndsToken(content[start - 1]))
        {
            start--;
        }

        return Math.Min(start, Math.Max(0, content.Length - 1));
    }

    /// <summary>Decodes content, joining the streams of an array with nothing between them.</summary>
    /// <param name="contents">The resolved content.</param>
    /// <param name="buffer">Receives the bytes.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns><see langword="false"/> when the content is not a stream or array, or does not decode.</returns>
    private static bool TryDecode(PdfValue contents, ref PooledBuffer buffer, CancellationToken cancellationToken)
    {
        try
        {
            return Decode(contents, ref buffer, cancellationToken);
        }
        catch (Exception ex) when (IsDecodeFailure(ex))
        {
            return false;
        }
    }

    /// <summary>Decodes content, joining the streams of an array with a carriage return.</summary>
    /// <param name="contents">The resolved content.</param>
    /// <param name="buffer">Receives the bytes.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns><see langword="false"/> when the content is not a stream or array.</returns>
    private static bool Decode(PdfValue contents, ref PooledBuffer buffer, CancellationToken cancellationToken)
    {
        if (contents.AsStream() is { } single)
        {
            AppendDecoded(single, ref buffer, cancellationToken);
            return true;
        }

        if (contents.AsArray() is not { } parts)
        {
            return false;
        }

        var first = true;
        for (var i = 0; i < parts.Count; i++)
        {
            if (parts.Get(i).AsStream() is not { } part)
            {
                continue;
            }

            if (!first)
            {
                buffer.WriteByte(PartSeparator);
            }

            first = false;
            AppendDecoded(part, ref buffer, cancellationToken);
        }

        return true;
    }

    /// <summary>Determines whether an exception is a stream that fails to decode, which veraPDF reads as empty content.</summary>
    /// <param name="ex">The exception.</param>
    /// <returns><see langword="true"/> for decoding failures.</returns>
    private static bool IsDecodeFailure(Exception ex) =>
        ex is InvalidDataException or PdfException or ArgumentException or InvalidOperationException or IndexOutOfRangeException
            or NotSupportedException or FormatException or OverflowException;

    /// <summary>
    /// Determines whether veraPDF decodes a stream's filters. It knows only the five general filters and an identity
    /// Crypt filter; any other filter, including the abbreviated names, gives it empty data.
    /// </summary>
    /// <param name="stream">The stream.</param>
    /// <param name="cancellationToken">Cancels a long filter list.</param>
    /// <returns><see langword="true"/> when every filter is one veraPDF decodes.</returns>
    private static bool HasReadableFilters(PdfStream stream, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        var filter = stream.Dictionary.Get(KnownName.Filter);
        if (filter.TryGetName(out var name))
        {
            return IsReadableFilter(name.ToKnownName());
        }

        var filters = filter.AsArray();
        for (var i = 0; filters is not null && i < filters.Count; i++)
        {
            if (i % CancelInterval == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            if (!IsReadableFilter(filters.GetName(i).ToKnownName()))
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Decodes one stream onto the end of a buffer; a stream veraPDF cannot decode adds nothing.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    private static void AppendDecoded(PdfStream stream, ref PooledBuffer buffer, CancellationToken cancellationToken)
    {
        if (!HasReadableFilters(stream, cancellationToken))
        {
            return;
        }

        var piece = default(PooledBuffer);
        try
        {
            _ = stream.Decode(ref piece, cancellationToken);
            buffer.Write(piece.WrittenSpan);
        }
        finally
        {
            piece.Dispose();
        }
    }
}
