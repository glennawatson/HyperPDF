// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Reads streams for the PDF/A rules, treating a stream that cannot be decoded as absent rather than failing the validation.</summary>
internal static class PdfAFontStreams
{
    /// <summary>Decodes a stream.</summary>
    /// <param name="stream">The stream.</param>
    /// <param name="cancellationToken">Cancels the decoding.</param>
    /// <param name="data">The decoded bytes, or empty when decoding failed.</param>
    /// <returns><see langword="true"/> when the stream decoded.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static bool TryDecode(PdfStream stream, CancellationToken cancellationToken, out byte[] data)
    {
        try
        {
            data = stream.DecodeToArray(cancellationToken);
            return true;
        }
        catch (Exception ex) when (IsReadFailure(ex))
        {
            data = [];
            return false;
        }
    }

    /// <summary>Determines whether an exception reports damaged data rather than a programming error or cancellation.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns><see langword="true"/> for the exceptions decoders and parsers throw on bad input.</returns>
    internal static bool IsReadFailure(Exception exception) => exception is InvalidDataException or
        PdfException or
        ArgumentException or
        InvalidOperationException or
        IndexOutOfRangeException or
        NotSupportedException or
        FormatException or
        OverflowException;
}
