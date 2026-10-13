// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Unicode;
using HyperPdf.Cms;

namespace HyperPdfLibrary.Signing;

/// <summary>
/// A file with a signature update appended and the room for the signature value reserved: the byte range is patched in
/// and the signed digest is taken, ready for the signature value to be written into the reserved space.
/// </summary>
[DebuggerDisplay("PdfSigningFile: {File.Length} bytes, {Capacity} reserved")]
internal sealed class PdfSigningFile
{
    /// <summary>The hexadecimal digits a byte takes.</summary>
    private const int DigitsPerByte = 2;

    /// <summary>The placeholder the writer wrote for the byte range.</summary>
    private static readonly byte[] RangePlaceholder = Encoding.ASCII.GetBytes(
        string.Create(
            CultureInfo.InvariantCulture,
            $"[0 {PdfSignatureDictionaryBuilder.PlaceholderFirst} {PdfSignatureDictionaryBuilder.PlaceholderSecond} {PdfSignatureDictionaryBuilder.PlaceholderThird}]"));

    /// <summary>Initializes a new instance of the <see cref="PdfSigningFile"/> class.</summary>
    /// <param name="file">The whole file.</param>
    /// <param name="updateStart">Where the update starts.</param>
    /// <param name="capacity">The room reserved for the signature value, in bytes.</param>
    private PdfSigningFile(byte[] file, int updateStart, int capacity)
    {
        File = file;
        UpdateStart = updateStart;
        Capacity = capacity;
    }

    /// <summary>Gets the whole file: the original bytes, the update, the byte range and (once written) the signature value.</summary>
    internal byte[] File { get; }

    /// <summary>Gets where the update starts, which is the length of the original file.</summary>
    internal int UpdateStart { get; }

    /// <summary>Gets the room reserved for the signature value, in bytes.</summary>
    internal int Capacity { get; }

    /// <summary>Gets the position of the opening angle bracket of the reserved value.</summary>
    internal int ContentsStart { get; private set; }

    /// <summary>Gets the position after the closing angle bracket of the reserved value.</summary>
    internal int ContentsEnd { get; private set; }

    /// <summary>Gets the key that precedes the reserved space.</summary>
    private static ReadOnlySpan<byte> ContentsKey => "/Contents <"u8;

    /// <summary>Finds the reserved space in a file just written and patches the byte range into it.</summary>
    /// <param name="file">The whole file.</param>
    /// <param name="updateStart">Where the update starts.</param>
    /// <param name="capacity">The room reserved for the signature value, in bytes.</param>
    /// <returns>The file ready for a signature.</returns>
    /// <exception cref="PdfException">The reserved space or the byte range is not where the update should have written it.</exception>
    internal static PdfSigningFile Prepare(byte[] file, int updateStart, int capacity)
    {
        var result = new PdfSigningFile(file, updateStart, capacity);
        result.LocateContents();
        result.PatchByteRange();
        return result;
    }

    /// <summary>Gets the bytes before the signature value.</summary>
    /// <returns>The first range.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<byte> Head() => File.AsSpan(0, ContentsStart);

    /// <summary>Gets the bytes after the signature value.</summary>
    /// <returns>The second range.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal ReadOnlySpan<byte> Tail() => File.AsSpan(ContentsEnd);

    /// <summary>Hashes the signed bytes: everything except the reserved space.</summary>
    /// <param name="digest">The digest.</param>
    /// <param name="destination">A buffer of at least <see cref="CmsDigests.MaxLength"/> bytes.</param>
    /// <returns>The digest length.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal int HashSigned(CmsDigestAlgorithm digest, Span<byte> destination) => PdfSigningDigests.Hash(digest, Head(), Tail(), destination);

    /// <summary>Writes the signature value into the reserved space, as hexadecimal digits followed by the zeros already there.</summary>
    /// <param name="value">The DER-encoded signature value.</param>
    /// <returns><see langword="true"/> when it was written; <see langword="false"/> when it is larger than the reserved space.</returns>
    internal bool TryWriteContents(ReadOnlySpan<byte> value)
    {
        if (value.Length > Capacity)
        {
            return false;
        }

        return Convert.TryToHexString(value, File.AsSpan(ContentsStart + 1, value.Length * DigitsPerByte), out _);
    }

    /// <summary>Finds the reserved space: an angle-bracketed run of zeros of exactly the reserved length in the update.</summary>
    /// <exception cref="PdfException">The space is not in the update.</exception>
    private void LocateContents()
    {
        var update = File.AsSpan(UpdateStart);
        var zeros = Capacity * DigitsPerByte;
        for (var searchFrom = 0; searchFrom < update.Length;)
        {
            var at = update[searchFrom..].IndexOf(ContentsKey);
            if (at < 0)
            {
                break;
            }

            var open = UpdateStart + searchFrom + at + ContentsKey.Length - 1;
            var close = open + 1 + zeros;
            if (close < File.Length && File[close] == (byte)'>' && File.AsSpan(open + 1, zeros).IndexOfAnyExcept((byte)'0') < 0)
            {
                ContentsStart = open;
                ContentsEnd = close + 1;
                return;
            }

            searchFrom += at + ContentsKey.Length;
        }

        throw new PdfException(PdfError.Format, "The room for the signature was not found in the update, so the file was not signed.");
    }

    /// <summary>Writes the real byte range over the placeholder, which has the same length.</summary>
    /// <exception cref="PdfException">The placeholder is not in the update.</exception>
    private void PatchByteRange()
    {
        var at = File.AsSpan(UpdateStart).IndexOf(RangePlaceholder);
        if (at < 0)
        {
            throw new PdfException(PdfError.Format, "The byte range was not found in the update, so the file was not signed.");
        }

        var target = File.AsSpan(UpdateStart + at, RangePlaceholder.Length);
        if (!Utf8.TryWrite(target, CultureInfo.InvariantCulture, $"[0 {ContentsStart:D10} {ContentsEnd:D10} {File.Length - ContentsEnd:D10}]", out var written) || written != target.Length)
        {
            throw new PdfException(PdfError.Format, "The byte range does not fit the room the update left for it, so the file was not signed.");
        }
    }
}
