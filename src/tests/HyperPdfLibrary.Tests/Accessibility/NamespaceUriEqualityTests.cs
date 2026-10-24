// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Accessibility;

/// <summary>Checks ordinal URI equality against PDF text decoding, including malformed Unicode.</summary>
public sealed class NamespaceUriEqualityTests
{
    /// <summary>The URI length that exceeds local character storage.</summary>
    private const int LongCharacters = 1_000;

    /// <summary>The PDFDocEncoding byte count.</summary>
    private const int ByteValues = 256;

    /// <summary>The bytes in a UTF-16 mark and code unit.</summary>
    private const int Utf16Bytes = 2;

    /// <summary>The unmarked-byte fast path relies on PDFDocEncoding's distinct decoded characters.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Equal_PdfDocEncoding_HasDistinctCharacters()
    {
        var characters = new HashSet<char>();
        for (var value = 0; value < ByteValues; value++)
        {
            _ = characters.Add(PdfText.DecodeDocByte((byte)value));
        }

        await Assert.That(characters.Count).IsEqualTo(ByteValues);
    }

    /// <summary>Equivalent URI values compare equally across all supported PDF text encodings.</summary>
    /// <param name="text">The URI value.</param>
    /// <param name="cancellationToken">Cancels URI comparison.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    [Arguments("urn:example:shared")]
    [Arguments("urn:example:\u2022\u0152")]
    [Arguments("urn:example:\u02D8\u02C7\u02DC")]
    [Arguments("urn:example:\u0017\u007F")]
    [Arguments("urn:example:\U0001F600")]
    [Arguments("")]
    public async Task Equal_DifferentEncodings_MatchesDecodedText(string text, CancellationToken cancellationToken)
    {
        var doc = PdfText.Encode(text);
        var utf8 = PdfText.EncodeUtf8(text);
        var bigEndian = EncodeUnicode(text, false);
        var littleEndian = EncodeUnicode(text, true);
        await Assert.That(NamespaceUriEquality.Equal(doc, utf8, cancellationToken)).IsTrue();
        await Assert.That(NamespaceUriEquality.Equal(doc, bigEndian, cancellationToken)).IsTrue();
        await Assert.That(NamespaceUriEquality.Equal(bigEndian, littleEndian, cancellationToken)).IsTrue();
    }

    /// <summary>Long URI storage is returned between equal and unequal comparisons.</summary>
    /// <param name="cancellationToken">Cancels URI comparison.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Equal_LongUris_DoesNotRetainWorkingValues(CancellationToken cancellationToken)
    {
        var text = new string('x', LongCharacters);
        var left = PdfText.Encode(text);
        var right = EncodeUnicode(text, false);
        var different = EncodeUnicode(new('y', LongCharacters), true);
        await Assert.That(NamespaceUriEquality.Equal(left, right, cancellationToken)).IsTrue();
        await Assert.That(NamespaceUriEquality.Equal(left, different, cancellationToken)).IsFalse();
        await Assert.That(NamespaceUriEquality.Equal(left, right, cancellationToken)).IsTrue();
    }

    /// <summary>Malformed encodings retain the decoder's replacement and odd-byte semantics.</summary>
    /// <param name="cancellationToken">Cancels URI comparison.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Equal_MalformedUnicode_MatchesDecoder(CancellationToken cancellationToken)
    {
        byte[][] encodings =
        [
            [0xFE, 0xFF, 0x00, 0x78, 0xFF],
            [0xFE, 0xFF, 0xD8, 0x00],
            [0xFF, 0xFE, 0x00, 0xD8],
            [0xEF, 0xBB, 0xBF, 0xFF],
            [0xEF, 0xBB, 0xBF, 0xE1, 0x80],
            [0xEF, 0xBB, 0xBF, 0xF0, 0x9F, 0x98],
            [0xEF, 0xBB, 0xBF, 0xFF, 0xFF],
            [0xFE, 0xFF, 0xD8, 0x00, 0xD8, 0x00, 0xDC, 0x00],
            [0xFF, 0xFE, 0x00, 0xDC, 0x00, 0xD8, 0xFF],
            [0xFE, 0xFF, 0xD8, 0x3D, 0xDE, 0x00],
            [0xEF, 0xBB, 0xBF, 0xF0, 0x9F, 0x98, 0x80],
            [0x78],
            [],
        ];
        foreach (var left in encodings)
        {
            foreach (var right in encodings)
            {
                var expected = string.Equals(PdfText.Decode(left), PdfText.Decode(right), StringComparison.Ordinal);
                await Assert.That(NamespaceUriEquality.Equal(left, right, cancellationToken)).IsEqualTo(expected);
            }
        }
    }

    /// <summary>Long malformed runs use replacement decoding without retaining pooled working text.</summary>
    /// <param name="cancellationToken">Cancels URI comparison.</param>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Equal_LongMalformedUnicode_ReturnsWorkingStorage(CancellationToken cancellationToken)
    {
        var left = new byte[Utf16Bytes + (LongCharacters * Utf16Bytes)];
        ReadOnlySpan<byte> mark = [0xFF, 0xFE];
        mark.CopyTo(left);
        for (var offset = Utf16Bytes; offset < left.Length; offset += Utf16Bytes)
        {
            left[offset + 1] = 0xD8;
        }

        var right = PdfText.EncodeUtf8(new('\uFFFD', LongCharacters));
        var different = PdfText.EncodeUtf8(new('x', LongCharacters));
        await Assert.That(NamespaceUriEquality.Equal(left, right, cancellationToken)).IsTrue();
        await Assert.That(NamespaceUriEquality.Equal(left, different, cancellationToken)).IsFalse();
        await Assert.That(NamespaceUriEquality.Equal(left, right, cancellationToken)).IsTrue();
    }

    /// <summary>Cancellation is observed even when equal raw bytes avoid decoding.</summary>
    /// <returns>The asynchronous test.</returns>
    [Test]
    public async Task Equal_CancelledToken_ThrowsBeforeFastPath()
    {
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();
        await Assert.That(() => NamespaceUriEquality.Equal("urn:example"u8, "urn:example"u8, cancelled.Token)).Throws<OperationCanceledException>();
    }

    /// <summary>Encodes text with an explicit UTF-16 byte-order mark.</summary>
    /// <param name="text">The URI value.</param>
    /// <param name="littleEndian">Whether to write little-endian code units.</param>
    /// <returns>The marked PDF text bytes.</returns>
    private static byte[] EncodeUnicode(string text, bool littleEndian)
    {
        var encoding = littleEndian ? Encoding.Unicode : Encoding.BigEndianUnicode;
        return [.. encoding.GetPreamble(), .. encoding.GetBytes(text)];
    }
}
