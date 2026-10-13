// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>Eight-pixel lanes for <see cref="CmykKernel"/>.</summary>
internal readonly record struct CmykVector256Lanes : ICmykLanes<Vector256<float>, Vector256<uint>>
{
    /// <summary>Gets the pixels processed per call.</summary>
    public static int Count => Vector256<uint>.Count;

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.Load"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<uint> Load(ref byte source, nuint byteOffset) => Vector256.LoadUnsafe(ref source, byteOffset).AsUInt32();

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.Store"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Store(Vector256<uint> pixels, ref byte destination, nuint byteOffset) => pixels.AsByte().StoreUnsafe(ref destination, byteOffset);

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.Channel"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Channel(Vector256<uint> pixels, int shift) =>
        Vector256.ConvertToSingle(Vector256.ShiftRightLogical(pixels, shift) & Vector256.Create((uint)byte.MaxValue)) * Vector256.Create(CmykKernel.InverseByteScale);

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.Splat"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Splat(float value) => Vector256.Create(value);

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.Multiply"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> Multiply(Vector256<float> left, Vector256<float> right) => left * right;

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.MultiplyAdd"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<float> MultiplyAdd(Vector256<float> left, Vector256<float> right, Vector256<float> addend) =>
        Vector256.FusedMultiplyAdd(left, right, addend);

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.Pack"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector256<uint> Pack(Vector256<float> red, Vector256<float> green, Vector256<float> blue) =>
        ToByte(blue) | Vector256.ShiftLeft(ToByte(green), CmykKernel.GreenShift) | Vector256.ShiftLeft(ToByte(red), CmykKernel.RedShift) | Vector256.Create(CmykKernel.OpaqueAlpha);

    /// <summary>Scales colours to 0..255 with a rounding bias and truncates, which rounds to the nearest byte.</summary>
    /// <param name="value">The colours from 0 to 1.</param>
    /// <returns>The bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector256<uint> ToByte(Vector256<float> value)
    {
        var scaled = Vector256.FusedMultiplyAdd(value, Vector256.Create(CmykKernel.ByteScale), Vector256.Create(CmykKernel.RoundingBias));
        return Vector256.ConvertToUInt32(Vector256.Min(Vector256.Max(scaled, Vector256<float>.Zero), Vector256.Create(CmykKernel.ByteScale)));
    }
}
