// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;

namespace HyperPdfLibrary.Metadata;

/// <summary>Collects text without copying a single XML text node.</summary>
internal static class XmpTextValues
{
    /// <summary>The capacity growth factor.</summary>
    private const int GrowthFactor = 2;

    /// <summary>Appends one node, leasing character storage only for fragmented text.</summary>
    /// <param name="value">The collected text.</param>
    /// <param name="text">The next text node.</param>
    /// <param name="cancellationToken">Cancels text collection before copying or renting.</param>
    /// <exception cref="PdfException">The combined text exceeds the supported length.</exception>
    internal static void Append(ref XmpTextValue value, string text, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (text.Length == 0)
        {
            return;
        }

        if (value.Single is null && value.Buffer is null)
        {
            value = new(text, null, 0);
            return;
        }

        var length = value.Buffer is null ? value.Single!.Length : value.Length;
        if (text.Length > PdfLimits.MaxDecodedLength - length)
        {
            throw new PdfException(PdfError.Format, "The XMP text exceeds the supported length.");
        }

        var required = length + text.Length;
        var buffer = value.Buffer;
        if (buffer is null || required > buffer.Length)
        {
            var capacity = buffer is null ? required : Math.Max(required, Math.Min((long)buffer.Length * GrowthFactor, PdfLimits.MaxDecodedLength));
            var larger = ArrayPool<char>.Shared.Rent((int)capacity);
            if (buffer is null)
            {
                value.Single.AsSpan().CopyTo(larger);
            }
            else
            {
                buffer.AsSpan(0, length).CopyTo(larger);
                ArrayPool<char>.Shared.Return(buffer);
            }

            buffer = larger;
        }

        text.AsSpan().CopyTo(buffer.AsSpan(length));
        value = new(null, buffer, required);
    }

    /// <summary>Gets the collected text, returning the original node when it was not fragmented.</summary>
    /// <param name="value">The collected text.</param>
    /// <returns>The text, or an empty string when no node arrived.</returns>
    internal static string GetText(in XmpTextValue value) =>
        value.Buffer is null ? value.Single ?? string.Empty : new string(value.Buffer, 0, value.Length);

    /// <summary>Returns leased characters and empties the value.</summary>
    /// <param name="value">The collected text.</param>
    internal static void Release(ref XmpTextValue value)
    {
        if (value.Buffer is { } buffer)
        {
            ArrayPool<char>.Shared.Return(buffer);
        }

        value = default;
    }
}
