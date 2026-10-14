// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>Tests for <see cref="AsciiHexFilter"/> encoding.</summary>
public sealed class AsciiHexFilterTests
{
    /// <summary>The most hexadecimal digits on a line.</summary>
    private const int MaxLineLength = 72;

    /// <summary>The bytes left when "4865GG6C" has its stray characters skipped.</summary>
    private static readonly byte[] DigitsBeforeAndAfterTheFault = [0x48, 0x65, 0x6C];

    /// <summary>The encoder gives upper-case digits and the '&gt;' end marker.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_Hello_GivesUpperCaseDigitsAndMarker() =>
        await Assert.That(Encode("Hello"u8)).IsEqualTo("48656C6C6F>");

    /// <summary>Empty data is only the end marker.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_Empty_IsEndMarker() => await Assert.That(Encode([])).IsEqualTo(">");

    /// <summary>Every sample decodes back to itself.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_Samples_RoundTrip()
    {
        foreach (var sample in FilterSamples.All())
        {
            var encoded = FilterRoundTrip.Encode(static (input, ref output) => AsciiHexFilter.Encode(input, ref output), sample.Data);
            var decoded = FilterRoundTrip.Decode(KnownName.ASCIIHexDecode, encoded);

            await Assert.That(decoded).IsEquivalentTo(sample.Data).Because(sample.Name);
        }
    }

    /// <summary>Lines stay at 72 digits, and the abbreviated name decodes the text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_LongInput_WrapsLines()
    {
        var sample = FilterSamples.Get("ramp");
        var encoded = FilterRoundTrip.Encode(static (input, ref output) => AsciiHexFilter.Encode(input, ref output), sample.Data);
        foreach (var line in Encoding.ASCII.GetString(encoded).Split('\n'))
        {
            await Assert.That(line.Length).IsLessThanOrEqualTo(MaxLineLength + 1);
        }

        await Assert.That(FilterRoundTrip.Decode(KnownName.AHx, encoded)).IsEquivalentTo(sample.Data);
    }

    /// <summary>Digits, white space, an odd last digit and the end marker decode clean.</summary>
    /// <param name="text">The encoded text.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("48656C6C6F>")]
    [Arguments("48 65 6c\n6C 6F 4>")]
    [Arguments("48656C6C6F")]
    [Arguments(">")]
    public async Task TryDecode_WellFormedText_IsClean(string text) =>
        await Assert.That(TryDecode(text)).IsTrue();

    /// <summary>A character that is not a digit or white space before the end marker makes the data damaged (7.4.2), and the rest still decodes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TryDecode_StrayCharacter_IsDamagedButKeepsTheDigits()
    {
        await Assert.That(TryDecode("4865GG6C>")).IsFalse();
        await Assert.That(DecodedPart("4865GG6C>")).IsEquivalentTo(DigitsBeforeAndAfterTheFault);
    }

    /// <summary>Decodes text and returns what was decoded, whether or not it was clean.</summary>
    /// <param name="text">The encoded text.</param>
    /// <returns>The decoded bytes.</returns>
    private static byte[] DecodedPart(string text)
    {
        var output = default(PooledBuffer);
        try
        {
            _ = AsciiHexFilter.TryDecode(Encoding.Latin1.GetBytes(text), ref output);
            return output.ToArray();
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Tells whether text decodes clean.</summary>
    /// <param name="text">The encoded text.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool TryDecode(string text)
    {
        var output = default(PooledBuffer);
        try
        {
            return AsciiHexFilter.TryDecode(Encoding.Latin1.GetBytes(text), ref output);
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Encodes bytes to text.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The hexadecimal text.</returns>
    private static string Encode(ReadOnlySpan<byte> data) =>
        Encoding.ASCII.GetString(FilterRoundTrip.Encode(static (input, ref output) => AsciiHexFilter.Encode(input, ref output), data));
}
