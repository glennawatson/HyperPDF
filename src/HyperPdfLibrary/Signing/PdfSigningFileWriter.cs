// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text.Unicode;
using HyperPdf.Cms;
using HyperPdfLibrary.Filters;

namespace HyperPdfLibrary.Signing;

/// <summary>Prepares bounded signature reservations and patches their byte range and contents.</summary>
internal static class PdfSigningFileWriter
{
    /// <summary>The hexadecimal digits a byte takes.</summary>
    private const int DigitsPerByte = 2;

    /// <summary>The opening and closing delimiters of the reserved contents.</summary>
    private const int ContentsDelimiters = 2;

    /// <summary>Gets the exact placeholder written by the signature dictionary.</summary>
    private static ReadOnlySpan<byte> RangePlaceholder => "[0 1111111111 2222222222 3333333333]"u8;

    /// <summary>Gets the key preceding the reserved hexadecimal contents.</summary>
    private static ReadOnlySpan<byte> ContentsKey => "/Contents <"u8;

    /// <summary>Finds the reserved contents and patches the signed byte range.</summary>
    /// <param name="file">The whole file buffer, patched in place.</param>
    /// <param name="updateStart">The first byte of the incremental update.</param>
    /// <param name="capacity">The reserved signature bytes.</param>
    /// <param name="cancellationToken">Cancels preparation.</param>
    /// <returns>The prepared file snapshot.</returns>
    /// <exception cref="PdfException">Offsets, lengths or placeholders are invalid.</exception>
    internal static PdfSigningFile Prepare(byte[] file, int updateStart, int capacity, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if ((uint)updateStart > (uint)file.Length || capacity < 0 || capacity > (file.Length - updateStart - ContentsDelimiters) / DigitsPerByte)
        {
            throw new PdfException(PdfError.Format, "The signature reservation or incremental-update offset is outside the file.");
        }

        var start = LocateContents(file, updateStart, capacity, cancellationToken);
        var result = new PdfSigningFile(file, updateStart, capacity, start, start + (capacity * DigitsPerByte) + ContentsDelimiters);
        PatchByteRange(result);
        return result;
    }

    /// <summary>Hashes every byte except the reserved signature contents.</summary>
    /// <param name="file">The prepared file.</param>
    /// <param name="digest">The digest algorithm.</param>
    /// <param name="destination">Receives the digest.</param>
    /// <param name="scratchPools">The caller's native-context owner.</param>
    /// <param name="cancellationToken">Cancels hashing.</param>
    /// <returns>The digest length.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int HashSigned(PdfSigningFile file, CmsDigestAlgorithm digest, Span<byte> destination, ScratchPools scratchPools, CancellationToken cancellationToken) =>
        PdfSigningDigests.Hash(digest, file.File.AsSpan(0, file.ContentsStart), file.File.AsSpan(file.ContentsEnd), destination, scratchPools, cancellationToken);

    /// <summary>Writes fitting contents without changing either signed range.</summary>
    /// <param name="file">The prepared file.</param>
    /// <param name="value">The DER signature bytes.</param>
    /// <param name="cancellationToken">Cancels before writing.</param>
    /// <returns>Whether the value fits and was written.</returns>
    internal static bool TryWriteContents(PdfSigningFile file, ReadOnlySpan<byte> value, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        return value.Length <= file.Capacity
            && Convert.TryToHexString(value, file.File.AsSpan(file.ContentsStart + 1, value.Length * DigitsPerByte), out _);
    }

    /// <summary>Finds an exactly sized zero-filled reservation inside the update.</summary>
    /// <param name="file">The file bytes.</param>
    /// <param name="updateStart">The update offset.</param>
    /// <param name="capacity">The reserved bytes.</param>
    /// <param name="cancellationToken">Cancels the scan.</param>
    /// <returns>The opening delimiter offset.</returns>
    /// <exception cref="PdfException">The reservation is absent.</exception>
    private static int LocateContents(ReadOnlySpan<byte> file, int updateStart, int capacity, CancellationToken cancellationToken)
    {
        var update = file[updateStart..];
        var zeros = capacity * DigitsPerByte;
        for (var searchFrom = 0; searchFrom < update.Length;)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var at = update[searchFrom..].IndexOf(ContentsKey);
            if (at < 0)
            {
                break;
            }

            var open = updateStart + searchFrom + at + ContentsKey.Length - 1;
            if (zeros < file.Length - open - 1 && file[open + 1 + zeros] == (byte)'>' && file.Slice(open + 1, zeros).IndexOfAnyExcept((byte)'0') < 0)
            {
                return open;
            }

            searchFrom += at + ContentsKey.Length;
        }

        throw new PdfException(PdfError.Format, "The room for the signature was not found in the update, so the file was not signed.");
    }

    /// <summary>Patches the real byte range over the fixed-width placeholder.</summary>
    /// <param name="file">The prepared reservation.</param>
    /// <exception cref="PdfException">The placeholder is absent or cannot hold the byte range.</exception>
    private static void PatchByteRange(PdfSigningFile file)
    {
        var at = file.File.AsSpan(file.UpdateStart).IndexOf(RangePlaceholder);
        if (at < 0)
        {
            throw new PdfException(PdfError.Format, "The byte range was not found in the update, so the file was not signed.");
        }

        var target = file.File.AsSpan(file.UpdateStart + at, RangePlaceholder.Length);
        var fits = Utf8.TryWrite(target, CultureInfo.InvariantCulture, $"[0 {file.ContentsStart:D10} {file.ContentsEnd:D10} {file.File.Length - file.ContentsEnd:D10}]", out var written);
        if (!fits || written != target.Length)
        {
            throw new PdfException(PdfError.Format, "The byte range does not fit the room the update left for it, so the file was not signed.");
        }
    }
}
