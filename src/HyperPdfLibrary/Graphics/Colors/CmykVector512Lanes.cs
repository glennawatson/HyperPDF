// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>Sixteen-pixel lanes for <see cref="CmykKernel"/>.</summary>
internal readonly record struct CmykVector512Lanes : ICmykLanes<Vector512<float>, Vector512<uint>>
{
    /// <summary>Gets the pixels processed per call.</summary>
    public static int Count => Vector512<uint>.Count;

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.Load"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<uint> Load(ref byte source, nuint byteOffset) => Vector512.LoadUnsafe(ref source, byteOffset).AsUInt32();

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.Store"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Store(Vector512<uint> pixels, ref byte destination, nuint byteOffset) => pixels.AsByte().StoreUnsafe(ref destination, byteOffset);

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.Channel"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Channel(Vector512<uint> pixels, int shift) =>
        Vector512.ConvertToSingle(Vector512.ShiftRightLogical(pixels, shift) & Vector512.Create((uint)byte.MaxValue)) * Vector512.Create(CmykKernel.InverseByteScale);

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.Splat"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Splat(float value) => Vector512.Create(value);

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.Multiply"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> Multiply(Vector512<float> left, Vector512<float> right) => left * right;

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.MultiplyAdd"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<float> MultiplyAdd(Vector512<float> left, Vector512<float> right, Vector512<float> addend) =>
        Vector512.FusedMultiplyAdd(left, right, addend);

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.Pack"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static Vector512<uint> Pack(Vector512<float> red, Vector512<float> green, Vector512<float> blue) =>
        ToByte(blue) | Vector512.ShiftLeft(ToByte(green), CmykKernel.GreenShift) | Vector512.ShiftLeft(ToByte(red), CmykKernel.RedShift) | Vector512.Create(CmykKernel.OpaqueAlpha);

    /// <summary>Scales colours to 0..255 with a rounding bias and truncates, which rounds to the nearest byte.</summary>
    /// <param name="value">The colours from 0 to 1.</param>
    /// <returns>The bytes.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static Vector512<uint> ToByte(Vector512<float> value)
    {
        var scaled = Vector512.FusedMultiplyAdd(value, Vector512.Create(CmykKernel.ByteScale), Vector512.Create(CmykKernel.RoundingBias));
        return Vector512.ConvertToUInt32(Vector512.Min(Vector512.Max(scaled, Vector512<float>.Zero), Vector512.Create(CmykKernel.ByteScale)));
    }
}
