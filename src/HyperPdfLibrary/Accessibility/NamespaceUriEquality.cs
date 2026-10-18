// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Buffers.Binary;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.Unicode;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Compares namespace URI text across PDF string encodings without retaining decoded strings.</summary>
internal static class NamespaceUriEquality
{
    /// <summary>The character capacity kept on the stack for each URI.</summary>
    private const int StackCharacters = 256;

    /// <summary>The first low PDFDocEncoding character that differs from ASCII.</summary>
    private const byte LowSpecialFirst = 0x18;

    /// <summary>The last low PDFDocEncoding character that differs from ASCII.</summary>
    private const byte LowSpecialLast = 0x1F;

    /// <summary>The first high PDFDocEncoding character that differs from Latin-1.</summary>
    private const byte HighSpecialFirst = 0x80;

    /// <summary>The last high PDFDocEncoding character that differs from Latin-1.</summary>
    private const byte HighSpecialLast = 0xA0;

    /// <summary>The bytes in a UTF-16 code unit and its byte-order mark.</summary>
    private const int Utf16Bytes = 2;

    /// <summary>The bytes in the UTF-8 byte-order mark.</summary>
    private const int Utf8MarkBytes = 3;

    /// <summary>The first UTF-16 surrogate code unit.</summary>
    private const char SurrogateFirst = '\uD800';

    /// <summary>The last UTF-16 surrogate code unit.</summary>
    private const char SurrogateLast = '\uDFFF';

    /// <summary>The decoder replacement for an unpaired surrogate.</summary>
    private const char ReplacementCharacter = '\uFFFD';

    /// <summary>The code units in a surrogate pair.</summary>
    private const int SurrogatePairCharacters = 2;

    /// <summary>The interval mask for cancellation in malformed UTF-16 runs.</summary>
    private const int CancellationMask = 255;

    /// <summary>Compares the decoded UTF-16 values with ordinal semantics.</summary>
    /// <param name="left">The first encoded URI.</param>
    /// <param name="right">The second encoded URI.</param>
    /// <param name="cancellationToken">Cancels comparison and replacement decoding.</param>
    /// <returns>Whether the two encodings name the same namespace.</returns>
    internal static bool Equal(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (left.SequenceEqual(right))
        {
            return true;
        }

        // PDFDocEncoding maps each byte to a distinct character, so unequal unmarked bytes cannot name the same URI.
        if (!HasUnicodeMark(left) && !HasUnicodeMark(right))
        {
            return false;
        }

        var leftLength = DecodedCapacity(left);
        var rightLength = DecodedCapacity(right);
        if (leftLength != rightLength && !HasUtf8Mark(left) && !HasUtf8Mark(right))
        {
            return false;
        }

        return EqualDecoded(left, right, leftLength, rightLength, cancellationToken);
    }

    /// <summary>Recognises the PDF text-string Unicode byte-order marks.</summary>
    /// <param name="bytes">The encoded URI.</param>
    /// <returns>Whether Unicode decoding is required.</returns>
    internal static bool HasUnicodeMark(ReadOnlySpan<byte> bytes) =>
        HasUtf16Mark(bytes) || HasUtf8Mark(bytes);

    /// <summary>Compares replacement-decoded text in stack or leased storage.</summary>
    /// <param name="left">The first encoded URI.</param>
    /// <param name="right">The second encoded URI.</param>
    /// <param name="leftLength">The first URI's decoded capacity.</param>
    /// <param name="rightLength">The second URI's decoded capacity.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>Whether the decoded values are ordinally equal.</returns>
    private static bool EqualDecoded(ReadOnlySpan<byte> left, ReadOnlySpan<byte> right, int leftLength, int rightLength, CancellationToken cancellationToken)
    {
        char[]? leftLease = null;
        char[]? rightLease = null;
        try
        {
            Span<char> leftText = leftLength <= StackCharacters ? stackalloc char[StackCharacters] : (leftLease = ArrayPool<char>.Shared.Rent(leftLength));
            Span<char> rightText = rightLength <= StackCharacters ? stackalloc char[StackCharacters] : (rightLease = ArrayPool<char>.Shared.Rent(rightLength));
            var leftWritten = Decode(left, leftText, cancellationToken);
            var rightWritten = Decode(right, rightText, cancellationToken);
            return leftText[..leftWritten].SequenceEqual(rightText[..rightWritten]);
        }
        finally
        {
            if (leftLease is not null)
            {
                ArrayPool<char>.Shared.Return(leftLease);
            }

            if (rightLease is not null)
            {
                ArrayPool<char>.Shared.Return(rightLease);
            }
        }
    }

    /// <summary>Recognises either UTF-16 byte order.</summary>
    /// <param name="bytes">The encoded URI.</param>
    /// <returns>Whether the URI has a UTF-16 mark.</returns>
    private static bool HasUtf16Mark(ReadOnlySpan<byte> bytes) =>
        bytes.StartsWith<byte>([0xFE, 0xFF]) || bytes.StartsWith<byte>([0xFF, 0xFE]);

    /// <summary>Recognises a UTF-8 marked PDF text string.</summary>
    /// <param name="bytes">The encoded URI.</param>
    /// <returns>Whether the URI has a UTF-8 mark.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool HasUtf8Mark(ReadOnlySpan<byte> bytes) => bytes.StartsWith<byte>([0xEF, 0xBB, 0xBF]);

    /// <summary>Bounds decoded storage without allocating fallback buffers for malformed input.</summary>
    /// <param name="bytes">The encoded URI.</param>
    /// <returns>The exact UTF-16/PDFDocEncoding length or an upper bound for UTF-8.</returns>
    private static int DecodedCapacity(ReadOnlySpan<byte> bytes)
    {
        if (HasUtf16Mark(bytes))
        {
            return (bytes.Length - Utf16Bytes) / Utf16Bytes;
        }

        return HasUtf8Mark(bytes) ? bytes.Length - Utf8MarkBytes : bytes.Length;
    }

    /// <summary>Decodes into caller storage without creating decoder fallback objects.</summary>
    /// <param name="bytes">The encoded URI.</param>
    /// <param name="destination">The decoded character storage.</param>
    /// <param name="cancellationToken">Cancels replacement decoding.</param>
    /// <returns>The characters written.</returns>
    /// <exception cref="PdfException">The decoded output exceeds its character bound.</exception>
    private static int Decode(ReadOnlySpan<byte> bytes, Span<char> destination, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (HasUtf16Mark(bytes))
        {
            return DecodeUtf16(bytes, destination, cancellationToken);
        }

        if (HasUtf8Mark(bytes))
        {
            var status = Utf8.ToUtf16(bytes[Utf8MarkBytes..], destination, out var consumed, out var written);
            if (status != OperationStatus.Done || consumed != bytes.Length - Utf8MarkBytes)
            {
                throw new PdfException("The namespace URI could not be decoded within its character bound.");
            }

            return written;
        }

        return bytes.IndexOfAnyInRange(LowSpecialFirst, LowSpecialLast) < 0 && bytes.IndexOfAnyInRange(HighSpecialFirst, HighSpecialLast) < 0
            ? Encoding.Latin1.GetChars(bytes, destination)
            : PdfText.Decode(bytes, destination);
    }

    /// <summary>Swaps code-unit bytes in bulk and replaces only unpaired surrogates.</summary>
    /// <param name="bytes">The marked UTF-16 URI.</param>
    /// <param name="destination">The decoded character storage.</param>
    /// <param name="cancellationToken">Cancels surrogate replacement.</param>
    /// <returns>The characters written, excluding an odd trailing byte.</returns>
    private static int DecodeUtf16(ReadOnlySpan<byte> bytes, Span<char> destination, CancellationToken cancellationToken)
    {
        var count = (bytes.Length - Utf16Bytes) / Utf16Bytes;
        var body = bytes.Slice(Utf16Bytes, count * Utf16Bytes);
        var characters = destination[..count];
        var littleEndian = bytes.StartsWith<byte>([0xFF, 0xFE]);
        if (littleEndian == BitConverter.IsLittleEndian)
        {
            body.CopyTo(MemoryMarshal.AsBytes(characters));
        }
        else
        {
            BinaryPrimitives.ReverseEndianness(MemoryMarshal.Cast<byte, ushort>(body), MemoryMarshal.Cast<char, ushort>(characters));
        }

        ReplaceUnpairedSurrogates(characters, cancellationToken);
        return count;
    }

    /// <summary>Keeps paired surrogates and substitutes the decoder's replacement for lone code units.</summary>
    /// <param name="characters">The decoded code units.</param>
    /// <param name="cancellationToken">Cancels long malformed runs.</param>
    private static void ReplaceUnpairedSurrogates(Span<char> characters, CancellationToken cancellationToken)
    {
        var steps = 0;

        // Unsigned code units select the vectorised range search without cold-path character boxing.
        while (MemoryMarshal.Cast<char, ushort>(characters).IndexOfAnyInRange((ushort)SurrogateFirst, (ushort)SurrogateLast) is >= 0 and var offset)
        {
            if ((steps & CancellationMask) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            steps++;
            characters = characters[offset..];
            if (char.IsHighSurrogate(characters[0]) && characters.Length > 1 && char.IsLowSurrogate(characters[1]))
            {
                characters = characters[SurrogatePairCharacters..];
                continue;
            }

            characters[0] = ReplacementCharacter;
            characters = characters[1..];
        }
    }
}
