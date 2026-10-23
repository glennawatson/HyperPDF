// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Filters;

/// <summary>Tests for <see cref="Ascii85Filter"/> encoding.</summary>
public sealed class Ascii85FilterTests
{
    /// <summary>The most characters on a line, with the line feed.</summary>
    private const int MaxLineLength = 76;

    /// <summary>The first character a base-85 digit can be.</summary>
    private const char FirstDigit = '!';

    /// <summary>The last character a base-85 digit can be.</summary>
    private const char LastDigit = 'u';

    /// <summary>The encoder gives the base-85 text of a known sentence.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_KnownSentence_GivesKnownText()
    {
        var encoded = Encode("Man is distinguished"u8);

        await Assert.That(encoded).IsEqualTo("9jqo^BlbD-BleB1DJ+*+F(f,q~>");
    }

    /// <summary>Four zero bytes become a single 'z', and a partial group never does.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_ZeroGroups_UseZButPartialGroupsDoNot()
    {
        await Assert.That(Encode(new byte[4])).IsEqualTo("z~>");
        await Assert.That(Encode(new byte[8])).IsEqualTo("zz~>");
        await Assert.That(Encode(new byte[3])).IsEqualTo("!!!!~>");
        await Assert.That(Encode(new byte[1])).IsEqualTo("!!~>");
    }

    /// <summary>Empty data is only the end marker.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_Empty_IsEndMarker() => await Assert.That(Encode([])).IsEqualTo("~>");

    /// <summary>A final group of one, two or three bytes gives two, three or four characters (n + 1).</summary>
    /// <param name="length">The bytes.</param>
    /// <param name="characters">The characters before the end marker.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(1, 2)]
    [Arguments(2, 3)]
    [Arguments(3, 4)]
    [Arguments(4, 5)]
    [Arguments(5, 7)]
    public async Task Encode_PartialGroups_UseNPlusOneCharacters(int length, int characters)
    {
        var data = new byte[length];
        Array.Fill(data, (byte)0xFF);

        await Assert.That(Encode(data).Length).IsEqualTo(characters + "~>".Length);
    }

    /// <summary>Every sample decodes back to itself.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_Samples_RoundTrip()
    {
        foreach (var sample in FilterSamples.All())
        {
            var encoded = FilterRoundTrip.Encode(static (input, ref output) => Ascii85Filter.Encode(input, ref output), sample.Data);
            var decoded = FilterRoundTrip.Decode(KnownName.ASCII85Decode, encoded);

            await Assert.That(decoded).IsEquivalentTo(sample.Data).Because(sample.Name);
        }
    }

    /// <summary>Lines stay short, and the text holds only digit characters, 'z', white space and the end marker.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Encode_LongInput_WrapsAndUsesOnlyLegalCharacters()
    {
        var sample = FilterSamples.Get("noise");
        var text = Encode(sample.Data);
        foreach (var line in text.Split('\n'))
        {
            await Assert.That(line.Length).IsLessThanOrEqualTo(MaxLineLength);
        }

        var body = text[..^"~>".Length];
        await Assert.That(text.EndsWith("~>", StringComparison.Ordinal)).IsTrue();
        await Assert.That(body.All(static c => c is '\n' or 'z' or (>= FirstDigit and <= LastDigit))).IsTrue();
    }

    /// <summary>Data that follows ISO 32000-2 7.4.3 decodes clean: digits, 'z', white space, an optional prefix and the end marker, or a '~' cut off at the end.</summary>
    /// <param name="text">The encoded text.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("9jqo^BlbD-BleB1DJ+*+F(f,q~>")]
    [Arguments("<~9jqo^BlbD-BleB1DJ+*+F(f,q~>")]
    [Arguments("9jqo^ Blb\nD-BleB1DJ+*+F(f,q~>")]
    [Arguments("z~>")]
    [Arguments("s8W-!~>")]
    [Arguments("9jqo^BlbD-BleB1DJ+*+F(f,q")]
    [Arguments("9jqo^BlbD-BleB1DJ+*+F(f,q~")]
    [Arguments("")]
    public async Task TryDecode_WellFormedText_IsClean(string text) =>
        await Assert.That(TryDecode(text)).IsTrue();

    /// <summary>
    /// Data that breaks the rules of 7.4.3 and its erratum decodes damaged: a '~' without '&gt;', a stray character, a 'z'
    /// inside a group, a group above 2^32 - 1 or a final group of one character.
    /// </summary>
    /// <param name="text">The encoded text.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("9jqo^BlbD~x")]
    [Arguments("9jqo^Blb\u0001D~>")]
    [Arguments("9jqo^Blb{D~>")]
    [Arguments("9jqoz~>")]
    [Arguments("uuuuu~>")]
    [Arguments("9jqo^B~>")]
    public async Task TryDecode_BrokenText_IsDamaged(string text) =>
        await Assert.That(TryDecode(text)).IsFalse();

    /// <summary>Damaged data still decodes the part before the fault.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TryDecode_DamagedText_KeepsTheDecodedPart() =>
        await Assert.That(DecodedPart("9jqo^BlbD-Blb\u0001e~>")).IsEqualTo("Man is dis");

    /// <summary>Decodes text and returns what was decoded, whether or not it was clean.</summary>
    /// <param name="text">The encoded text.</param>
    /// <returns>The decoded text.</returns>
    private static string DecodedPart(string text)
    {
        var output = default(PooledBuffer);
        try
        {
            _ = Ascii85Filter.TryDecode(Encoding.Latin1.GetBytes(text), ref output);
            return Encoding.ASCII.GetString(output.WrittenSpan);
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
            return Ascii85Filter.TryDecode(Encoding.Latin1.GetBytes(text), ref output);
        }
        finally
        {
            output.Dispose();
        }
    }

    /// <summary>Encodes bytes to text.</summary>
    /// <param name="data">The bytes.</param>
    /// <returns>The base-85 text.</returns>
    private static string Encode(ReadOnlySpan<byte> data) =>
        Encoding.ASCII.GetString(FilterRoundTrip.Encode(static (input, ref output) => Ascii85Filter.Encode(input, ref output), data));
}
