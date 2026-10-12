// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Decodes streams for the PDF/A checks, treating a damaged stream as empty.</summary>
internal static class PdfAStreams
{
    /// <summary>Decodes a stream through every filter except image codecs.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="output">The buffer receiving the data.</param>
    /// <param name="cancellationToken">Stops the decoding.</param>
    /// <returns><see langword="true"/> when the stream decoded; <see langword="false"/> when it is damaged.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static bool TryDecode(PdfStream stream, ref PooledBuffer output, CancellationToken cancellationToken)
    {
        try
        {
            _ = stream.Decode(ref output, cancellationToken);
            return true;
        }
        catch (Exception ex) when (ex is InvalidDataException or
        PdfException or
        ArgumentException or
        InvalidOperationException or
        IndexOutOfRangeException or
        NotSupportedException or
        FormatException or
        OverflowException)
        {
            output.Length = 0;
            return false;
        }
    }
}
