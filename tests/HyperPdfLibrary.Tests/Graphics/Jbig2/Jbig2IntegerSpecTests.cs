// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Filters;
using HyperPdfLibrary.Graphics.Images.Jbig2;
using HyperPdfLibrary.Graphics.Images.Jbig2.Encoding;

namespace HyperPdfLibrary.Tests.Graphics.Jbig2;

/// <summary>
/// Checks the arithmetic integer decoding procedures of annex A of the JBIG2 standard: each value of table A.1 is written as the
/// bits the table gives, coded with the context rule of A.2 (PREV), and decoded with the library's decoder.
/// </summary>
public sealed class Jbig2IntegerSpecTests
{
    /// <summary>The first size guess for the coded data.</summary>
    private const int InitialOutput = 64;

    /// <summary>The value of PREV from which the rightmost nine bits are used.</summary>
    private const int PrevLimit = 256;

    /// <summary>The mask of the rightmost nine bits.</summary>
    private const int PrevMask = 511;

    /// <summary>The ones a prefix can have before it needs no terminating zero.</summary>
    private const int LongestPrefix = 5;

    /// <summary>The symbol ID length of the example in A.3.</summary>
    private const int ExampleCodeLength = 3;

    /// <summary>The bits of the example in A.3: the symbol ID 010.</summary>
    private const string ExampleIdBits = "010";

    /// <summary>The symbol ID the example in A.3 gives.</summary>
    private const int ExampleId = 2;

    /// <summary>The value of the example in A.2: the bits 0101000 are the value 12.</summary>
    private const int ExampleValue = 12;

    /// <summary>The bits of the example in A.2.</summary>
    private const string ExampleBits = "0101000";

    /// <summary>The bits of the out-of-band row of table A.1.</summary>
    private const string OutOfBandBits = "1000";

    /// <summary>The bits of the row of table A.1 that holds -1 alone.</summary>
    private const string MinusOneBits = "1001";

    /// <summary>The prefix of the row of table A.1 that holds -3 and -2.</summary>
    private const string MinusThreePrefix = "101";

    /// <summary>The base of the binary number system.</summary>
    private const int Binary = 2;

    /// <summary>The value bits of the -3 and -2 row.</summary>
    private const int MinusThreeBits = 1;

    /// <summary>The value of the first magnitude of the -3 and -2 row.</summary>
    private const long MinusThreeBase = 2;

    /// <summary>The prefixes of the rows of table A.1 for values that are not negative.</summary>
    private static readonly string[] PositivePrefixes = ["00", "010", "0110", "01110", "011110", "011111"];

    /// <summary>The prefixes of the rows of table A.1 for negative values.</summary>
    private static readonly string[] NegativePrefixes = [MinusThreePrefix, "110", "1110", "11110", "111110", "111111"];

    /// <summary>The first magnitude of each row of table A.1.</summary>
    private static readonly long[] Offsets = [0, 4, 20, 84, 340, 4436];

    /// <summary>The bits that follow the prefix of each row of table A.1.</summary>
    private static readonly int[] ValueBits = [2, 4, 6, 8, 12, 32];

    /// <summary>The values that cover every row of table A.1 at both ends; null stands for out-of-band.</summary>
    private static readonly int?[] Values =
    [
        0, 3, -1, -2, -3, 4, 19, -4, -19, 20, 83, -20, -83, 84, 339, -84, -339, 340, 4435, -340, -4435, 4436, 100_000, -4436, -100_000, int.MaxValue, null,
    ];

    /// <summary>Every row of table A.1 decodes to its value, in one run so the contexts adapt across values.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EveryRowOfTableA1Decodes()
    {
        var bits = string.Concat(Values.Select(TableA1Bits));

        await Assert.That(DecodeIntegers(bits, Values.Length)).IsEquivalentTo(Values);
    }

    /// <summary>The example of A.2: the bits 0101000 are the value 12.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TheA2ExampleDecodesToTwelve()
    {
        await Assert.That(DecodeIntegers(ExampleBits, 1)[0]).IsEqualTo(ExampleValue);
        await Assert.That(TableA1Bits(ExampleValue)).IsEqualTo(ExampleBits);
    }

    /// <summary>The example of A.3: with a symbol ID length of 3, the bits 0, 1, 0 are the symbol ID 2.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TheA3ExampleDecodesToTwo()
    {
        var encoder = new Jbig2MqEncoder(InitialOutput);
        var contexts = new byte[1 << ExampleCodeLength];
        var previous = 1;
        foreach (var bit in ExampleIdBits)
        {
            var value = bit - '0';
            encoder.Encode(ref contexts[previous], value);
            previous = (previous << 1) | value;
        }

        var data = Finish(ref encoder);
        var decoder = new Jbig2ArithmeticDecoder(data, 0);

        await Assert.That(Jbig2IntegerDecoder.DecodeId(ref decoder, new byte[1 << ExampleCodeLength], ExampleCodeLength)).IsEqualTo(ExampleId);
    }

    /// <summary>Gives the bits table A.1 assigns to a value.</summary>
    /// <param name="value">The value, or null for out-of-band.</param>
    /// <returns>The bits as text.</returns>
    private static string TableA1Bits(int? value)
    {
        if (value is not { } v)
        {
            return OutOfBandBits;
        }

        if (v == -1)
        {
            return MinusOneBits;
        }

        var negative = v < 0;
        var magnitude = Math.Abs((long)v);
        var row = Array.FindLastIndex(Offsets, offset => magnitude >= offset);
        var prefix = negative ? NegativePrefixes[row] : PositivePrefixes[row];
        var bits = ValueBits[row];
        var offset = Offsets[row];
        if (negative && row == 0)
        {
            // The row for -3 and -2 has one value bit, counting from 2.
            bits = MinusThreeBits;
            offset = MinusThreeBase;
        }

        return prefix + Convert.ToString(magnitude - offset, Binary).PadLeft(bits, '0');
    }

    /// <summary>Finishes an encoder.</summary>
    /// <param name="encoder">The encoder.</param>
    /// <returns>The coded bytes.</returns>
    private static byte[] Finish(ref Jbig2MqEncoder encoder)
    {
        var output = default(PooledBuffer);
        encoder.Finish(ref output);
        var data = output.ToArray();
        output.Dispose();
        return data;
    }

    /// <summary>Codes bits with the context rule of A.2 and decodes the integers with the library.</summary>
    /// <param name="bits">The bits of every integer, one after another.</param>
    /// <param name="count">The number of integers.</param>
    /// <returns>The decoded integers; null for out-of-band.</returns>
    private static int?[] DecodeIntegers(string bits, int count)
    {
        var encoder = new Jbig2MqEncoder(InitialOutput);
        var contexts = new byte[Jbig2IntegerDecoder.ContextCount];
        var position = 0;
        for (var i = 0; i < count; i++)
        {
            position = EncodeOne(ref encoder, contexts, bits, position);
        }

        var decoder = new Jbig2ArithmeticDecoder(Finish(ref encoder), 0);
        var decodeContexts = new byte[Jbig2IntegerDecoder.ContextCount];
        var result = new int?[count];
        for (var i = 0; i < count; i++)
        {
            result[i] = Jbig2IntegerDecoder.Decode(ref decoder, decodeContexts, out var value) ? value : null;
        }

        return result;
    }

    /// <summary>Codes the bits of one integer, taking as many as figure A.1 gives it.</summary>
    /// <param name="encoder">The encoder.</param>
    /// <param name="contexts">The integer's contexts.</param>
    /// <param name="bits">All the bits.</param>
    /// <param name="start">Where the integer's bits start.</param>
    /// <returns>Where the next integer's bits start.</returns>
    private static int EncodeOne(ref Jbig2MqEncoder encoder, byte[] contexts, string bits, int start)
    {
        // The sign, then ones ended by a zero (no zero after five ones), then the value bits.
        var length = 1;
        var ones = 0;
        while (ones < LongestPrefix && bits[start + length] == '1')
        {
            ones++;
            length++;
        }

        length += ones < LongestPrefix ? 1 : 0;
        length += ValueBits[ones];
        var previous = 1;
        for (var i = 0; i < length; i++)
        {
            var bit = bits[start + i] - '0';
            encoder.Encode(ref contexts[previous], bit);
            previous = previous < PrevLimit ? (previous << 1) | bit : (((previous << 1) | bit) & PrevMask) | PrevLimit;
        }

        return start + length;
    }
}
