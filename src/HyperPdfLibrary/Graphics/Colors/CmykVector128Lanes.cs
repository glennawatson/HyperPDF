// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>Four-pixel lanes for <see cref="CmykKernel"/>.</summary>
internal readonly record struct CmykVector128Lanes : ICmykLanes<Vector128<float>, Vector128<uint>>
{
    /// <summary>Gets the pixels processed per call.</summary>
    public static int Count => Vector128<uint>.Count;

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.Load"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<uint> Load(ref byte source, nuint byteOffset) => Vector128.LoadUnsafe(ref source, byteOffset).AsUInt32();

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.Store"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Store(Vector128<uint> pixels, ref byte destination, nuint byteOffset) => pixels.AsByte().StoreUnsafe(ref destination, byteOffset);

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.Channel"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> Channel(Vector128<uint> pixels, int shift) =>
        Vector128.ConvertToSingle(Vector128.ShiftRightLogical(pixels, shift) & Vector128.Create((uint)byte.MaxValue)) * Vector128.Create(CmykKernel.InverseByteScale);

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.Splat"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> Splat(float value) => Vector128.Create(value);

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.Multiply"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> Multiply(Vector128<float> left, Vector128<float> right) => left * right;

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.MultiplyAdd"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<float> MultiplyAdd(Vector128<float> left, Vector128<float> right, Vector128<float> addend) =>
        Vector128.FusedMultiplyAdd(left, right, addend);

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.Pack"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector128<uint> Pack(Vector128<float> red, Vector128<float> green, Vector128<float> blue) =>
        ToByte(blue) | Vector128.ShiftLeft(ToByte(green), CmykKernel.GreenShift) | Vector128.ShiftLeft(ToByte(red), CmykKernel.RedShift) | Vector128.Create(CmykKernel.OpaqueAlpha);

    /// <summary>Scales colours to 0..255 with a rounding bias and truncates, which rounds to the nearest byte.</summary>
    /// <param name="value">The colours from 0 to 1.</param>
    /// <returns>The bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector128<uint> ToByte(Vector128<float> value)
    {
        var scaled = Vector128.FusedMultiplyAdd(value, Vector128.Create(CmykKernel.ByteScale), Vector128.Create(CmykKernel.RoundingBias));
        return Vector128.ConvertToUInt32(Vector128.Min(Vector128.Max(scaled, Vector128<float>.Zero), Vector128.Create(CmykKernel.ByteScale)));
    }
}
