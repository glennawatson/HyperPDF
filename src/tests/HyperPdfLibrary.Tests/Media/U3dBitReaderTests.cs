// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.TestAssets;
using HyperPdfLibrary.Media.U3d;

namespace HyperPdfLibrary.Tests.Media;

/// <summary>
/// Checks the bit reader against the encoder of ECMA-363 clause 10.4: whatever the encoder writes, the reader must read back, in plain
/// and arithmetic form, through adaptive and static contexts, escapes, histogram halving and truncated data.
/// </summary>
public sealed class U3dBitReaderTests
{
    /// <summary>The seed of the repeatable number generator.</summary>
    private const uint Seed = 20_261_102;

    /// <summary>The multiplier of the repeatable number generator.</summary>
    private const uint Multiplier = 1_664_525;

    /// <summary>The increment of the repeatable number generator.</summary>
    private const uint Increment = 1_013_904_223;

    /// <summary>The values written in the long run.</summary>
    private const int LongRun = 30_000;

    /// <summary>The values written in the short runs.</summary>
    private const int ShortRun = 400;

    /// <summary>The number of different kinds of value a run mixes.</summary>
    private const int Kinds = 8;

    /// <summary>The range of the narrow static context.</summary>
    private const uint NarrowRange = 7;

    /// <summary>The range of the wide static context.</summary>
    private const uint WideRange = 5_000;

    /// <summary>A range too large for a static context, so its values are written plain.</summary>
    private const uint HugeRange = 20_000;

    /// <summary>The spread of the skewed adaptive values: most are one of a few small numbers.</summary>
    private const uint SmallValues = 6;

    /// <summary>Every this many skewed values, one is a large number that forces an escape.</summary>
    private const uint EscapeEvery = 97;

    /// <summary>The adaptive contexts the runs use.</summary>
    private const uint ContextCount = 5;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits in half of a 32-bit value.</summary>
    private const int HalfWordBits = 16;

    /// <summary>The value a 16-bit write is checked with.</summary>
    private const ushort SixteenBit = 0xBEEF;

    /// <summary>The value a 32-bit write is checked with.</summary>
    private const uint ThirtyTwoBit = 0xDEADBEEF;

    /// <summary>The float a float write is checked with.</summary>
    private const float Float = -12.5F;

    /// <summary>The double a double write is checked with.</summary>
    private const double Double = 1234.5678;

    /// <summary>The text a string write is checked with.</summary>
    private const string Text = "Grüße ☃";

    /// <summary>The kind of value an operation writes.</summary>
    private enum Kind
    {
        /// <summary>An 8-bit value.</summary>
        Byte = 0,

        /// <summary>A 16-bit value.</summary>
        Short = 1,

        /// <summary>A 32-bit value.</summary>
        Word = 2,

        /// <summary>A float.</summary>
        Float = 3,

        /// <summary>A value in an adaptive context with a narrow spread.</summary>
        Skewed = 4,

        /// <summary>A value in an adaptive context with a wide spread.</summary>
        Spread = 5,

        /// <summary>A value in a static context.</summary>
        Static = 6,

        /// <summary>A value in a static context too large to compress.</summary>
        Huge = 7,
    }

    /// <summary>Plain values of every type are read back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsPlainValues()
    {
        var bytes = new U3dValueWriter(true).U8(1).U16(SixteenBit).U32(ThirtyTwoBit).I32(-1).F32(Float).F64(Double).String(Text).ToArray();

        var result = ReadEverything(bytes, null);

        await Assert.That(result).IsEqualTo($"1 BEEF DEADBEEF -1 -12.5 1234.5678 {Text}");
    }

    /// <summary>The same values are read back from the arithmetic code, which holds no compressed values here and so is the plain bytes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsArithmeticValues()
    {
        var bytes = new U3dValueWriter(false).U8(1).U16(SixteenBit).U32(ThirtyTwoBit).I32(-1).F32(Float).F64(Double).String(Text).ToArray();

        var result = ReadEverything(bytes, new());

        await Assert.That(result).IsEqualTo($"1 BEEF DEADBEEF -1 -12.5 1234.5678 {Text}");
    }

    /// <summary>A section of only plain values is exactly the little-endian bytes, whichever form the writer uses.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PlainValuesAreTheSameBytesInBothForms()
    {
        var arithmetic = new U3dValueWriter(false).U32(ThirtyTwoBit).F32(Float).String(Text).ToArray();
        var plain = new U3dValueWriter(true).U32(ThirtyTwoBit).F32(Float).String(Text).ToArray();

        await Assert.That(arithmetic).IsEquivalentTo(plain);
    }

    /// <summary>A long mixed run of values is read back exactly from the arithmetic code, which covers escapes, growth, halving and underflow.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsALongMixedRun() => await Assert.That(FirstMismatch(Run(Seed, LongRun), false)).IsEqualTo(-1);

    /// <summary>Many short runs from different seeds are read back exactly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsManyShortRuns()
    {
        var failures = 0;
        for (uint seed = 1; seed <= ShortRun; seed++)
        {
            failures += FirstMismatch(Run(seed, ShortRun), false) == -1 ? 0 : 1;
        }

        await Assert.That(failures).IsEqualTo(0);
    }

    /// <summary>A file that is not compressed reads the same values, with the compressed fields written as plain 32-bit values.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsAPlainRun() => await Assert.That(FirstMismatch(Run(Seed, ShortRun), true)).IsEqualTo(-1);

    /// <summary>Data cut short sets the failed flag and reads zeros, and never throws.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TruncationFailsWithoutThrowing()
    {
        var bytes = new U3dValueWriter(false).U32(ThirtyTwoBit).ToArray();

        await Assert.That(ReadsShort(bytes.AsSpan(0, bytes.Length - 1), null)).IsTrue();
        await Assert.That(ReadsShort([], new())).IsTrue();
    }

    /// <summary>A string whose length runs past the data fails instead of reading beyond it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OverlongStringFails()
    {
        var bytes = new U3dValueWriter(true).U16(ushort.MaxValue).ToArray();
        var reader = new U3dBitReader(bytes, null);

        var text = reader.ReadString();
        var failed = reader.Failed;

        await Assert.That(text).IsEmpty();
        await Assert.That(failed).IsTrue();
    }

    /// <summary>A string longer than the stack buffer is read in full.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsALongString()
    {
        var text = new string('x', ByteBits * ByteBits * ByteBits);
        var bytes = new U3dValueWriter(true).String(text).ToArray();
        var reader = new U3dBitReader(bytes, null);

        var read = reader.ReadString();

        await Assert.That(read).IsEqualTo(text);
    }

    /// <summary>A static context with no values is refused, because it would divide by zero.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EmptyStaticContextFails()
    {
        var reader = new U3dBitReader(new byte[ByteBits * ByteBits], new());

        var value = reader.ReadCompressedU32(U3dContexts.Range(0));
        var failed = reader.Failed;

        await Assert.That(value).IsEqualTo(0U);
        await Assert.That(failed).IsTrue();
    }

    /// <summary>Reading after a failure keeps giving zero.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsAfterAFailureGiveZero()
    {
        var reader = new U3dBitReader([], null);
        reader.Fail();

        var value = reader.ReadU8();
        var remaining = reader.RemainingBytes;
        var position = reader.BytePosition;

        await Assert.That(value).IsEqualTo((byte)0);
        await Assert.That(remaining).IsEqualTo(0);
        await Assert.That(position).IsEqualTo(0);
    }

    /// <summary>The vector and matrix readers read their floats in order.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsVectorsAndMatrices()
    {
        var matrix = System.Numerics.Matrix4x4.CreateTranslation(System.Numerics.Vector3.One);
        var bytes = new U3dValueWriter(true).Vector3(System.Numerics.Vector3.UnitY).Vector4(System.Numerics.Vector4.UnitW).Matrix(matrix).F64(Double).ToArray();
        var reader = new U3dBitReader(bytes, null);

        var vector3 = reader.ReadVector3();
        var vector4 = reader.ReadVector4();
        var read = reader.ReadMatrix();
        var number = reader.ReadF64();
        var remaining = reader.RemainingBytes;

        await Assert.That(vector3).IsEqualTo(System.Numerics.Vector3.UnitY);
        await Assert.That(vector4).IsEqualTo(System.Numerics.Vector4.UnitW);
        await Assert.That(read).IsEqualTo(matrix);
        await Assert.That(number).IsEqualTo(Double);
        await Assert.That(remaining).IsEqualTo(0);
    }

    /// <summary>The context for a range uses the static form for small ranges and the plain form for large ones.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RangeContextsSwitchToPlainForHugeRanges()
    {
        await Assert.That(U3dContexts.Range(NarrowRange)).IsEqualTo(U3dContexts.StaticBase + NarrowRange);
        await Assert.That(U3dContexts.Range(HugeRange)).IsEqualTo(U3dContexts.StaticLimit);
    }

    /// <summary>Makes a repeatable run of operations.</summary>
    /// <param name="seed">The seed.</param>
    /// <param name="count">The number of operations.</param>
    /// <returns>The operations.</returns>
    private static Operation[] Run(uint seed, int count)
    {
        var state = seed;
        var operations = new Operation[count];
        for (var i = 0; i < count; i++)
        {
            state = unchecked((state * Multiplier) + Increment);
            var kind = (Kind)((state >> ByteBits) % Kinds);
            state = unchecked((state * Multiplier) + Increment);
            operations[i] = new(kind, Value(kind, state, i), (state >> ByteBits) % ContextCount);
        }

        return operations;
    }

    /// <summary>Chooses the value of an operation.</summary>
    /// <param name="kind">The kind of value.</param>
    /// <param name="state">A random number.</param>
    /// <param name="index">The operation's index.</param>
    /// <returns>The value.</returns>
    private static uint Value(Kind kind, uint state, int index) => kind switch
    {
        Kind.Byte => state & byte.MaxValue,
        Kind.Short => state & ushort.MaxValue,
        Kind.Skewed => index % EscapeEvery == 0 ? state : state % SmallValues,
        Kind.Spread => state >> HalfWordBits,
        Kind.Static => state % NarrowRange,
        Kind.Huge => state % HugeRange,
        _ => state,
    };

    /// <summary>Writes operations and reads them back.</summary>
    /// <param name="operations">The operations.</param>
    /// <param name="plain">Whether the file is not compressed.</param>
    /// <returns>The index of the first value that did not read back, or -1 when all did.</returns>
    private static int FirstMismatch(Operation[] operations, bool plain)
    {
        var writer = new U3dValueWriter(plain, new U3dWriterContexts());
        foreach (var operation in operations)
        {
            Write(writer, operation);
        }

        var reader = new U3dBitReader(writer.ToArray(), plain ? null : new U3dContextSet());
        for (var i = 0; i < operations.Length; i++)
        {
            if (ReadBack(ref reader, operations[i]) != operations[i].Value || reader.Failed)
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>Writes one operation.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="operation">The operation.</param>
    private static void Write(U3dValueWriter writer, Operation operation)
    {
        switch (operation.Kind)
        {
            case Kind.Byte:
            {
                _ = writer.U8((byte)operation.Value);
                break;
            }

            case Kind.Short:
            {
                _ = writer.U16((ushort)operation.Value);
                break;
            }

            case Kind.Float:
            {
                _ = writer.F32(BitConverter.UInt32BitsToSingle(operation.Value));
                break;
            }

            case Kind.Skewed or Kind.Spread:
            {
                _ = writer.Adaptive(1 + operation.Context, operation.Value);
                break;
            }

            case Kind.Static:
            {
                _ = writer.Ranged(NarrowRange, operation.Value);
                break;
            }

            case Kind.Huge:
            {
                _ = writer.Ranged(HugeRange, operation.Value);
                break;
            }

            default:
            {
                _ = writer.U32(operation.Value);
                break;
            }
        }
    }

    /// <summary>Reads one operation's value.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="operation">The operation.</param>
    /// <returns>The value read, as the number the operation wrote.</returns>
    private static uint ReadBack(ref U3dBitReader reader, Operation operation) => operation.Kind switch
    {
        Kind.Byte => reader.ReadU8(),
        Kind.Short => reader.ReadU16(),
        Kind.Float => BitConverter.SingleToUInt32Bits(reader.ReadF32()),
        Kind.Skewed or Kind.Spread => reader.ReadCompressedU32(1 + operation.Context),
        Kind.Static => reader.ReadCompressedU32(U3dContexts.Range(NarrowRange)),
        Kind.Huge => reader.ReadCompressedU32(U3dContexts.Range(HugeRange)),
        _ => reader.ReadU32(),
    };

    /// <summary>Reads a 32-bit value from data that is too short and tells whether the reader gave up.</summary>
    /// <param name="bytes">The data.</param>
    /// <param name="contexts">The contexts, or <see langword="null"/> for a plain section.</param>
    /// <returns><see langword="true"/> when the read failed and gave zero.</returns>
    private static bool ReadsShort(ReadOnlySpan<byte> bytes, U3dContextSet? contexts)
    {
        var reader = new U3dBitReader(bytes, contexts);
        return reader.ReadU32() == 0 && reader.Failed;
    }

    /// <summary>Reads one of each plain value type and writes them as text.</summary>
    /// <param name="bytes">The data.</param>
    /// <param name="contexts">The contexts, or <see langword="null"/> for a plain section.</param>
    /// <returns>The values, separated by spaces.</returns>
    private static string ReadEverything(byte[] bytes, U3dContextSet? contexts)
    {
        var reader = new U3dBitReader(bytes, contexts);
        var a = reader.ReadU8();
        var b = reader.ReadU16();
        var c = reader.ReadU32();
        var d = reader.ReadI32();
        var e = reader.ReadF32();
        var f = reader.ReadF64();
        var g = reader.ReadString();
        return string.Create(System.Globalization.CultureInfo.InvariantCulture, $"{a} {b:X} {c:X} {d} {e} {f} {g}");
    }

    /// <summary>One value written to and read from the code.</summary>
    /// <param name="Kind">The kind of value.</param>
    /// <param name="Value">The value.</param>
    /// <param name="Context">A number that picks the adaptive context.</param>
    private readonly record struct Operation(Kind Kind, uint Value, uint Context);
}
