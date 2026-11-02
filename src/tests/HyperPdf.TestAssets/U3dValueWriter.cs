// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Text;

namespace HyperPdf.TestAssets;

/// <summary>
/// Writes the data section of a U3D block as ECMA-363 clause 10.4 describes: values become symbols of an arithmetic code,
/// least significant bit first, and a section that holds compressed values ends with a zero 32-bit value. A writer made for a file
/// whose profile says it is not compressed writes plain little-endian values instead. It follows the encoder in the standard, not
/// the decoder in the library, so a test that reads its output checks the decoder against the standard.
/// </summary>
public sealed class U3dValueWriter
{
    /// <summary>The highest value of the 16-bit probability range.</summary>
    private const uint FullRange = 0xFFFF;

    /// <summary>The most significant bit of the range.</summary>
    private const uint Half = 0x8000;

    /// <summary>The second most significant bit of the range.</summary>
    private const uint Quarter = 0x4000;

    /// <summary>The number of values a plain byte symbol can take.</summary>
    private const uint ByteValues = 256;

    /// <summary>The largest range a static context may have; a larger range is written as a plain value.</summary>
    private const uint MaxStaticRange = 0x3FFE;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The alignment of a data section.</summary>
    private const int WordBytes = 4;

    /// <summary>The bits in a 16-bit half of a 32-bit value.</summary>
    private const int HalfWordBits = 16;

    /// <summary>The adaptive counts shared by the blocks of one object.</summary>
    private readonly U3dWriterContexts _contexts;

    /// <summary>Whether values are plain bytes.</summary>
    private readonly bool _plain;

    /// <summary>The bytes written.</summary>
    private readonly List<byte> _bytes = [];

    /// <summary>The number of bits written to the arithmetic code.</summary>
    private int _bitCount;

    /// <summary>The lowest value of the probability range.</summary>
    private uint _low;

    /// <summary>The highest value of the probability range.</summary>
    private uint _high = FullRange;

    /// <summary>The bits decided but not yet written.</summary>
    private uint _underflow;

    /// <summary>Whether a compressed value was written, so the code needs a closing zero.</summary>
    private bool _compressed;

    /// <summary>Initializes a new instance of the <see cref="U3dValueWriter"/> class with a new set of adaptive counts.</summary>
    /// <param name="plain"><see langword="true"/> to write plain values for a file whose profile says it is not compressed.</param>
    public U3dValueWriter(bool plain)
        : this(plain, new())
    {
    }

    /// <summary>Initializes a new instance of the <see cref="U3dValueWriter"/> class.</summary>
    /// <param name="plain"><see langword="true"/> to write plain values for a file whose profile says it is not compressed.</param>
    /// <param name="contexts">The adaptive counts of the object.</param>
    public U3dValueWriter(bool plain, U3dWriterContexts contexts)
    {
        _plain = plain;
        _contexts = contexts;
    }

    /// <summary>Writes an unsigned 8-bit value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>This writer.</returns>
    public U3dValueWriter U8(byte value)
    {
        if (_plain)
        {
            _bytes.Add(value);
            return this;
        }

        WriteStatic(ByteValues, Swap(value));
        return this;
    }

    /// <summary>Writes an unsigned 16-bit value, low byte first.</summary>
    /// <param name="value">The value.</param>
    /// <returns>This writer.</returns>
    public U3dValueWriter U16(ushort value) => U8((byte)(value & 0xFF)).U8((byte)(value >> ByteBits));

    /// <summary>Writes an unsigned 32-bit value, low half first.</summary>
    /// <param name="value">The value.</param>
    /// <returns>This writer.</returns>
    public U3dValueWriter U32(uint value) => U16((ushort)(value & 0xFFFF)).U16((ushort)(value >> HalfWordBits));

    /// <summary>Writes a signed 32-bit value as its bit pattern.</summary>
    /// <param name="value">The value.</param>
    /// <returns>This writer.</returns>
    public U3dValueWriter I32(int value) => U32(unchecked((uint)value));

    /// <summary>Writes a 32-bit floating-point value as its bit pattern.</summary>
    /// <param name="value">The value.</param>
    /// <returns>This writer.</returns>
    public U3dValueWriter F32(float value) => U32(BitConverter.SingleToUInt32Bits(value));

    /// <summary>Writes a 64-bit floating-point value as its bit pattern, low half first.</summary>
    /// <param name="value">The value.</param>
    /// <returns>This writer.</returns>
    public U3dValueWriter F64(double value)
    {
        var bits = BitConverter.DoubleToUInt64Bits(value);
        return U32((uint)(bits & 0xFFFFFFFF)).U32((uint)(bits >> (ByteBits * WordBytes)));
    }

    /// <summary>Writes a string: a 16-bit byte count and the UTF-8 bytes.</summary>
    /// <param name="text">The text.</param>
    /// <returns>This writer.</returns>
    public U3dValueWriter String(string text)
    {
        var bytes = Encoding.UTF8.GetBytes(text);
        _ = U16((ushort)bytes.Length);
        foreach (var value in bytes)
        {
            _ = U8(value);
        }

        return this;
    }

    /// <summary>Writes three 32-bit floats.</summary>
    /// <param name="vector">The vector.</param>
    /// <returns>This writer.</returns>
    public U3dValueWriter Vector3(Vector3 vector) => F32(vector.X).F32(vector.Y).F32(vector.Z);

    /// <summary>Writes four 32-bit floats.</summary>
    /// <param name="vector">The vector.</param>
    /// <returns>This writer.</returns>
    public U3dValueWriter Vector4(Vector4 vector) => F32(vector.X).F32(vector.Y).F32(vector.Z).F32(vector.W);

    /// <summary>Writes sixteen 32-bit floats in the order the standard names A to P.</summary>
    /// <param name="matrix">The matrix, whose fields are written row by row.</param>
    /// <returns>This writer.</returns>
    public U3dValueWriter Matrix(Matrix4x4 matrix) =>
        F32(matrix.M11).F32(matrix.M12).F32(matrix.M13).F32(matrix.M14)
            .F32(matrix.M21).F32(matrix.M22).F32(matrix.M23).F32(matrix.M24)
            .F32(matrix.M31).F32(matrix.M32).F32(matrix.M33).F32(matrix.M34)
            .F32(matrix.M41).F32(matrix.M42).F32(matrix.M43).F32(matrix.M44);

    /// <summary>Writes an unsigned 32-bit value in an adaptive context.</summary>
    /// <param name="context">The adaptive context.</param>
    /// <param name="value">The value.</param>
    /// <returns>This writer.</returns>
    public U3dValueWriter Adaptive(uint context, uint value)
    {
        if (_plain)
        {
            return U32(value);
        }

        _compressed = true;
        var symbol = value + 1;
        if (_contexts.CountOf(context, symbol) == 0)
        {
            WriteDynamic(context, 0);
            _ = U32(value);
            _contexts.Add(context, symbol);
            return this;
        }

        WriteDynamic(context, symbol);
        return this;
    }

    /// <summary>Writes an unsigned 32-bit value known to be below a range, in a static context of that range.</summary>
    /// <param name="range">The number of values the field can hold.</param>
    /// <param name="value">The value, below the range.</param>
    /// <returns>This writer.</returns>
    public U3dValueWriter Ranged(uint range, uint value)
    {
        if (_plain || range > MaxStaticRange)
        {
            return U32(value);
        }

        _compressed = true;
        WriteStatic(range, value);
        return this;
    }

    /// <summary>Finishes the section: closes the arithmetic code when it holds compressed values and pads it to whole 32-bit words.</summary>
    /// <returns>The bytes of the data section.</returns>
    public byte[] ToArray()
    {
        var wasCompressed = _compressed;
        if (wasCompressed)
        {
            _compressed = false;
            _ = U32(0);
        }

        // A section with only plain values is exactly those bytes; one with compressed values is a whole number of 32-bit words.
        while (wasCompressed && _bytes.Count % WordBytes != 0)
        {
            _bytes.Add(0);
        }

        return [.. _bytes];
    }

    /// <summary>Reverses the order of the bits of a byte, which the standard does before it writes an uncompressed value.</summary>
    /// <param name="value">The byte.</param>
    /// <returns>The byte with its bits reversed.</returns>
    private static uint Swap(byte value)
    {
        uint result = 0;
        for (var i = 0; i < ByteBits; i++)
        {
            result |= (uint)((value >> i) & 1) << (ByteBits - 1 - i);
        }

        return result;
    }

    /// <summary>Writes a value in a static context: every value below the range is equally likely.</summary>
    /// <param name="range">The range.</param>
    /// <param name="value">The value.</param>
    private void WriteStatic(uint range, uint value) => Narrow(value, 1, range);

    /// <summary>Writes a symbol of an adaptive context.</summary>
    /// <param name="context">The context.</param>
    /// <param name="symbol">The symbol, where 0 is the escape symbol.</param>
    private void WriteDynamic(uint context, uint symbol)
    {
        Narrow(_contexts.CountBelow(context, symbol), _contexts.CountOf(context, symbol), _contexts.Total(context));
        _contexts.Add(context, symbol);
    }

    /// <summary>Narrows the probability range to a symbol's share and writes the bits that become certain (ECMA-363 10.4.4.2 to 10.4.4.5).</summary>
    /// <param name="below">The sum of the counts of the symbols below this one.</param>
    /// <param name="count">The symbol's count.</param>
    /// <param name="total">The total of all counts.</param>
    private void Narrow(uint below, uint count, uint total)
    {
        var range = _high - _low + 1;
        _high = _low + (range * (below + count) / total) - 1;
        _low += range * below / total;
        while (((_high ^ _low) & Half) == 0)
        {
            var bit = (_high & Half) != 0 ? 1U : 0U;
            Bit(bit);
            _high = ((_high & ~Half) << 1) | 1;
            _low = (_low & ~Half) << 1;
            for (; _underflow != 0; _underflow--)
            {
                Bit(bit ^ 1);
            }
        }

        while ((_low & Quarter) != 0 && (_high & Quarter) == 0)
        {
            _high = (((_high & ~Half) << 1) | 1) | Half;
            _low = (_low & ~Half) << 1 & ~Half;
            _underflow++;
        }
    }

    /// <summary>Appends one bit, least significant bit of each byte first.</summary>
    /// <param name="bit">The bit.</param>
    private void Bit(uint bit)
    {
        if (_bitCount % ByteBits == 0)
        {
            _bytes.Add(0);
        }

        if (bit != 0)
        {
            _bytes[^1] |= (byte)(1 << (_bitCount % ByteBits));
        }

        _bitCount++;
    }
}
