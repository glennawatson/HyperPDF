// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>One-pixel lanes for <see cref="CmykKernel"/>: the scalar path and the tail of every vector row.</summary>
internal readonly record struct CmykScalarLanes : ICmykLanes<float, uint>
{
    /// <summary>Gets the pixels processed per call.</summary>
    public static int Count => 1;

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.Load"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Load(ref byte source, nuint byteOffset) =>
        BinaryPrimitives.ReadUInt32LittleEndian(System.Runtime.InteropServices.MemoryMarshal.CreateReadOnlySpan(ref Unsafe.Add(ref source, byteOffset), sizeof(uint)));

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.Store"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void Store(uint pixels, ref byte destination, nuint byteOffset) =>
        BinaryPrimitives.WriteUInt32LittleEndian(System.Runtime.InteropServices.MemoryMarshal.CreateSpan(ref Unsafe.Add(ref destination, byteOffset), sizeof(uint)), pixels);

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.Channel"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Channel(uint pixels, int shift) => ((pixels >> shift) & byte.MaxValue) * CmykKernel.InverseByteScale;

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.Splat"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Splat(float value) => value;

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.Multiply"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float Multiply(float left, float right) => left * right;

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.MultiplyAdd"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static float MultiplyAdd(float left, float right, float addend) => MathF.FusedMultiplyAdd(left, right, addend);

    /// <inheritdoc cref="ICmykLanes{TFloat, TWord}.Pack"/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static uint Pack(float red, float green, float blue) =>
        ToByte(blue) | (ToByte(green) << CmykKernel.GreenShift) | (ToByte(red) << CmykKernel.RedShift) | CmykKernel.OpaqueAlpha;

    /// <summary>Scales a colour to 0..255 with a rounding bias and truncates, which rounds to the nearest byte.</summary>
    /// <param name="value">The colour from 0 to 1.</param>
    /// <returns>The byte.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ToByte(float value) =>
        (uint)MathF.Min(MathF.Max(MathF.FusedMultiplyAdd(value, CmykKernel.ByteScale, CmykKernel.RoundingBias), 0F), CmykKernel.ByteScale);
}
