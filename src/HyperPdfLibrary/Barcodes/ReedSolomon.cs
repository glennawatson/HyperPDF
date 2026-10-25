// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Barcodes;

/// <summary>Works out Reed-Solomon error correction bytes over the field of 256 values that a barcode symbol uses.</summary>
/// <param name="Polynomial">The polynomial that reduces a product in the field: 0x11D for QR Code, 0x12D for Data Matrix.</param>
/// <param name="FirstRoot">The power of two where the generator's roots start: 0 for QR Code, 1 for Data Matrix.</param>
[DebuggerDisplay("ReedSolomon: polynomial {Polynomial:X}")]
internal readonly record struct ReedSolomon(int Polynomial, int FirstRoot)
{
    /// <summary>The field's size.</summary>
    private const int FieldSize = 256;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The value that stands for the power of two: the field element 2.</summary>
    private const int Two = 2;

    /// <summary>The most error correction bytes a symbol asks for, which sizes the divisor kept on the stack.</summary>
    private const int MaximumDegree = 68;

    /// <summary>Works out the error correction bytes for some data.</summary>
    /// <param name="data">The data bytes.</param>
    /// <param name="result">Receives the error correction bytes; its length is how many to make.</param>
    internal void GetRemainder(ReadOnlySpan<byte> data, Span<byte> result)
    {
        var degree = result.Length;
        Span<byte> divisor = stackalloc byte[MaximumDegree];
        divisor = divisor[..degree];
        FillDivisor(divisor);
        result.Clear();
        foreach (var value in data)
        {
            var factor = (byte)(value ^ result[0]);
            result[1..].CopyTo(result);
            result[degree - 1] = 0;
            for (var i = 0; i < degree; i++)
            {
                result[i] ^= Multiply(divisor[i], factor);
            }
        }
    }

    /// <summary>Multiplies two values of the field.</summary>
    /// <param name="x">The first value.</param>
    /// <param name="y">The second value.</param>
    /// <returns>The product.</returns>
    internal byte Multiply(byte x, byte y)
    {
        var product = 0;
        for (var bit = ByteBits - 1; bit >= 0; bit--)
        {
            product <<= 1;
            if (product >= FieldSize)
            {
                product ^= Polynomial;
            }

            if (((y >> bit) & 1) != 0)
            {
                product ^= x;
            }
        }

        return (byte)product;
    }

    /// <summary>Works out the generator polynomial's coefficients, highest power first, without the leading one.</summary>
    /// <param name="divisor">Receives the coefficients; its length is the polynomial's degree.</param>
    private void FillDivisor(Span<byte> divisor)
    {
        var degree = divisor.Length;
        divisor.Clear();
        divisor[degree - 1] = 1;
        byte root = 1;
        for (var power = 0; power < FirstRoot; power++)
        {
            root = Multiply(root, Two);
        }

        for (var i = 0; i < degree; i++)
        {
            for (var j = 0; j < degree; j++)
            {
                divisor[j] = Multiply(divisor[j], root);
                if (j + 1 < degree)
                {
                    divisor[j] ^= divisor[j + 1];
                }
            }

            root = Multiply(root, Two);
        }
    }
}
