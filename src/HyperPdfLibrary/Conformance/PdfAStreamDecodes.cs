// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Keeps the decoded bytes of the content streams one validation reads, so the rule areas that walk the same pages (graphics,
/// colour, fonts and file structure) inflate each stream once. A validation runs on one thread at a time, so this takes no
/// lock. It keeps no stream above <see cref="MaxStreamBytes"/> and no more than <see cref="MaxTotalBytes"/> in all; a stream
/// it does not keep is decoded again by each reader, as without it.
/// </summary>
[DebuggerDisplay("PdfAStreamDecodes: {_retained} bytes kept")]
internal sealed class PdfAStreamDecodes
{
    /// <summary>The most bytes of one stream's decoded data kept.</summary>
    internal const int MaxStreamBytes = 8 << 20;

    /// <summary>The most bytes of decoded data kept in all.</summary>
    internal const long MaxTotalBytes = 64L << 20;

    /// <summary>The decoded data by stream; <see langword="null"/> for a stream that failed to decode.</summary>
    private readonly Dictionary<PdfStream, byte[]?> _decoded = [with(ReferenceEqualityComparer.Instance)];

    /// <summary>The bytes kept so far.</summary>
    private long _retained;

    /// <summary>
    /// Decodes a <c>/Contents</c> value into one buffer as <see cref="ContentExecution.DecodeContents(PdfValue, ref PooledBuffer, CancellationToken)"/>
    /// does, reading each stream from the shared data when another rule area decoded it.
    /// </summary>
    /// <param name="contents">The <c>/Contents</c> value.</param>
    /// <param name="buffer">Receives the content.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <exception cref="InvalidDataException">A stream cannot be decoded.</exception>
    internal void AppendContents(PdfValue contents, ref PooledBuffer buffer, CancellationToken cancellationToken)
    {
        if (contents.AsStream() is { } single)
        {
            AppendPart(single, ref buffer, cancellationToken);
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

            AppendPart(part, ref buffer, cancellationToken);
            buffer.WriteByte(ContentExecution.StreamSeparator);
        }
    }

    /// <summary>Decodes a stream, or returns the data kept from an earlier read.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <param name="data">The decoded bytes, or empty when decoding failed. Readers must not change it.</param>
    /// <returns><see langword="true"/> when the stream decoded.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal bool TryGet(PdfStream stream, CancellationToken cancellationToken, out byte[] data)
    {
        if (_decoded.TryGetValue(stream, out var kept))
        {
            data = kept ?? [];
            return kept is not null;
        }

        if (!PdfAFontStreams.TryDecode(stream, cancellationToken, out data))
        {
            _decoded[stream] = null;
            return false;
        }

        if (data.Length <= MaxStreamBytes && _retained + data.Length <= MaxTotalBytes)
        {
            _decoded[stream] = data;
            _retained += data.Length;
        }

        return true;
    }

    /// <summary>Appends one stream's decoded data; a stream that fails to decode throws from the decoder, as without the shared data.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="buffer">The buffer.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    private void AppendPart(PdfStream stream, ref PooledBuffer buffer, CancellationToken cancellationToken)
    {
        if (TryGet(stream, cancellationToken, out var data))
        {
            buffer.Write(data);
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
