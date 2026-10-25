// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Graphics.Images.Jbig2;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2;

/// <summary>Tests for the MQ arithmetic decoder.</summary>
public sealed class Jbig2ArithmeticDecoderTests
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The index of the last bit in a byte.</summary>
    private const int LastBit = 7;

    /// <summary>The bytes of the marker that ends the coded data.</summary>
    private const int MarkerLength = 2;

    /// <summary>Gets the coded test sequence of T.88 annex H.2.</summary>
    private static ReadOnlySpan<byte> Coded =>
    [
        0x84, 0xC7, 0x3B, 0xFC, 0xE1, 0xA1, 0x43, 0x04, 0x02, 0x20, 0x00, 0x00, 0x41, 0x0D, 0xBB, 0x86, 0xF4, 0x31, 0x7F, 0xFF,
        0x88, 0xFF, 0x37, 0x47, 0x1A, 0xDB, 0x6A, 0xDF, 0xFF, 0xAC,
    ];

    /// <summary>Gets the decoded test sequence of T.88 annex H.2, coded with a single context.</summary>
    private static ReadOnlySpan<byte> Decoded =>
    [
        0x00, 0x02, 0x00, 0x51, 0x00, 0x00, 0x00, 0xC0, 0x03, 0x52, 0x87, 0x2A, 0xAA, 0xAA, 0xAA, 0xAA, 0x82, 0xC0, 0x20, 0x00,
        0xFC, 0xD7, 0x9E, 0xF6, 0xBF, 0x7F, 0xED, 0x90, 0x4F, 0x46, 0xA3, 0xBF,
    ];

    /// <summary>The decoder reproduces the standard's test sequence.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DecodesStandardTestSequence()
    {
        var bits = DecodeBits(Coded.ToArray(), Decoded.Length);

        await Assert.That(bits).IsEquivalentTo(Decoded.ToArray());
    }

    /// <summary>
    /// The decoder reports when it has read past the end of the data, but not while a 0xFF 0xAC marker holds it at
    /// the last byte, as in PDFium.
    /// </summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReportsCompletionPastTheEnd()
    {
        var withoutMarker = IsCompleteAfter(Coded[..^MarkerLength].ToArray(), Decoded.Length * ByteBits * ByteBits);
        var withMarker = IsCompleteAfter(Coded.ToArray(), Decoded.Length * ByteBits * ByteBits);

        await Assert.That(withoutMarker).IsTrue();
        await Assert.That(withMarker).IsFalse();
    }

    /// <summary>Decodes bits with one context, most significant first.</summary>
    /// <param name="coded">The coded data.</param>
    /// <param name="length">The bytes wanted.</param>
    /// <returns>The bytes.</returns>
    private static byte[] DecodeBits(byte[] coded, int length)
    {
        var decoder = new Jbig2ArithmeticDecoder(coded, 0);
        byte context = 0;
        var output = new byte[length];
        for (var i = 0; i < length * ByteBits; i++)
        {
            output[i / ByteBits] |= (byte)(decoder.Decode(ref context) << (LastBit - (i % ByteBits)));
        }

        return output;
    }

    /// <summary>Decodes many bits and reports whether the decoder ran out of data.</summary>
    /// <param name="coded">The coded data.</param>
    /// <param name="count">The bits decoded.</param>
    /// <returns>Whether the decoder is complete.</returns>
    private static bool IsCompleteAfter(byte[] coded, int count)
    {
        var decoder = new Jbig2ArithmeticDecoder(coded, 0);
        byte context = 0;
        for (var i = 0; i < count; i++)
        {
            _ = decoder.Decode(ref context);
        }

        return decoder.IsComplete;
    }
}
