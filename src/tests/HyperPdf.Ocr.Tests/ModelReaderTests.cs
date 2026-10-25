// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Ocr.Lstm;

namespace HyperPdf.Ocr.Tests;

/// <summary>Checks the reader of the values a recognition model is stored as.</summary>
public sealed class ModelReaderTests
{
    /// <summary>A 32 bit value with four different bytes.</summary>
    private const int Pattern = 0x01020304;

    /// <summary>A negative byte value.</summary>
    private const sbyte NegativeByte = -5;

    /// <summary>A 64 bit value that does not fit in 32 bits.</summary>
    private const long LargeValue = 0x1_0000_0002L;

    /// <summary>A float with an exact binary form.</summary>
    private const float ExactFloat = 1.5F;

    /// <summary>A double with an exact binary form.</summary>
    private const double ExactDouble = -0.25;

    /// <summary>A count that cannot fit in the bytes left.</summary>
    private const uint HugeCount = 1_000_000;

    /// <summary>Every kind of value reads back as it was written.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsEachKindOfValue()
    {
        var bytes = new ModelBytes().Int(Pattern).SByte(NegativeByte).Long(LargeValue).Float(ExactFloat).Double(ExactDouble).Text("héllo").ToArray();
        var read = ReadAll(bytes);

        await Assert.That(read.Integer).IsEqualTo(Pattern);
        await Assert.That(read.Signed).IsEqualTo(NegativeByte);
        await Assert.That(read.Long).IsEqualTo(LargeValue);
        await Assert.That(read.Single).IsEqualTo(ExactFloat);
        await Assert.That(read.Double).IsEqualTo(ExactDouble);
        await Assert.That(read.Text).IsEqualTo("héllo");
        await Assert.That(read.Remaining).IsEqualTo(0);
    }

    /// <summary>Reading past the end of the data throws a clear error rather than reading beyond it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ThrowsWhenTheDataEndsFirst()
    {
        var bytes = new ModelBytes().Byte(1).ToArray();

        await Assert.That(() => ReadTooMuch(bytes)).Throws<InvalidDataException>();
    }

    /// <summary>A stored count too big for the data left is refused before anything is allocated for it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesACountLargerThanTheData()
    {
        var bytes = new ModelBytes().UInt(HugeCount).ToArray();

        await Assert.That(() => ReadHugeString(bytes)).Throws<InvalidDataException>();
    }

    /// <summary>Doubles are narrowed to floats, which is how weights are held.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsDoublesAsFloats()
    {
        var bytes = new ModelBytes().Double(ExactFloat).Double(ExactDouble).ToArray();
        var values = new float[2];

        var remaining = ReadDoubles(bytes, values);

        await Assert.That(values[0]).IsEqualTo(ExactFloat);
        await Assert.That(values[1]).IsEqualTo((float)ExactDouble);
        await Assert.That(remaining).IsEqualTo(0);
    }

    /// <summary>Reads two 64 bit floats as 32 bit floats.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="values">Receives the values.</param>
    /// <returns>The number of bytes left.</returns>
    private static int ReadDoubles(byte[] bytes, float[] values)
    {
        var reader = new ModelReader(bytes);
        reader.ReadDoublesAsSingles(values);
        return reader.Remaining;
    }

    /// <summary>Reads one of every kind of value.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The values read.</returns>
    private static ValuesRead ReadAll(byte[] bytes)
    {
        var reader = new ModelReader(bytes);
        return new(reader.ReadInt32(), reader.ReadSByte(), reader.ReadInt64(), reader.ReadSingle(), reader.ReadDouble(), reader.ReadString(), reader.Remaining);
    }

    /// <summary>Reads past the end of one byte.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The value read.</returns>
    private static int ReadTooMuch(byte[] bytes)
    {
        var reader = new ModelReader(bytes);
        return reader.ReadInt32();
    }

    /// <summary>Reads a string whose length is larger than the data.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The string.</returns>
    private static string ReadHugeString(byte[] bytes)
    {
        var reader = new ModelReader(bytes);
        return reader.ReadString();
    }

    /// <summary>Values read from a model.</summary>
    /// <param name="Integer">A 32 bit integer.</param>
    /// <param name="Signed">A signed byte.</param>
    /// <param name="Long">A 64 bit integer.</param>
    /// <param name="Single">A 32 bit float.</param>
    /// <param name="Double">A 64 bit float.</param>
    /// <param name="Text">A string.</param>
    /// <param name="Remaining">The bytes left after reading.</param>
    private readonly record struct ValuesRead(int Integer, sbyte Signed, long Long, float Single, double Double, string Text, int Remaining);
}
