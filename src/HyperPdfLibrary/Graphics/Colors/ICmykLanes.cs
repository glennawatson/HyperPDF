// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// The lane operations the CMYK polynomial needs, so one body of code runs on a single pixel and on 128, 256 and 512-bit
/// vectors with identical arithmetic. Each implementation is a struct, so the generic code compiles to its own fast form.
/// </summary>
/// <typeparam name="TFloat">The float lanes.</typeparam>
/// <typeparam name="TWord">The 32-bit unsigned lanes: one CMYK or BGRA pixel per lane.</typeparam>
internal interface ICmykLanes<TFloat, TWord>
    where TFloat : unmanaged
    where TWord : unmanaged
{
    /// <summary>Gets the pixels processed per call.</summary>
    static abstract int Count { get; }

    /// <summary>Loads <see cref="Count"/> pixels of CMYK bytes; the cyan byte is the lowest.</summary>
    /// <param name="source">The first byte of the row.</param>
    /// <param name="byteOffset">The offset of the first pixel in bytes.</param>
    /// <returns>The pixels.</returns>
    static abstract TWord Load(ref byte source, nuint byteOffset);

    /// <summary>Stores <see cref="Count"/> BGRA pixels.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="destination">The first byte of the row.</param>
    /// <param name="byteOffset">The offset of the first pixel in bytes.</param>
    static abstract void Store(TWord pixels, ref byte destination, nuint byteOffset);

    /// <summary>Reads one byte of each pixel as a float from 0 to 1.</summary>
    /// <param name="pixels">The pixels.</param>
    /// <param name="shift">The bit position of the byte.</param>
    /// <returns>The channel.</returns>
    static abstract TFloat Channel(TWord pixels, int shift);

    /// <summary>Fills every lane with a value.</summary>
    /// <param name="value">The value.</param>
    /// <returns>The lanes.</returns>
    static abstract TFloat Splat(float value);

    /// <summary>Multiplies lane by lane.</summary>
    /// <param name="left">The left lanes.</param>
    /// <param name="right">The right lanes.</param>
    /// <returns>The products.</returns>
    static abstract TFloat Multiply(TFloat left, TFloat right);

    /// <summary>Computes <c>left * right + addend</c> with a single rounding.</summary>
    /// <param name="left">The left lanes.</param>
    /// <param name="right">The right lanes.</param>
    /// <param name="addend">The addend lanes.</param>
    /// <returns>The results.</returns>
    static abstract TFloat MultiplyAdd(TFloat left, TFloat right, TFloat addend);

    /// <summary>Clamps each colour to 0..1 and packs opaque BGRA words, rounding to the nearest byte.</summary>
    /// <param name="red">The red lanes.</param>
    /// <param name="green">The green lanes.</param>
    /// <param name="blue">The blue lanes.</param>
    /// <returns>The BGRA pixels.</returns>
    static abstract TWord Pack(TFloat red, TFloat green, TFloat blue);
}
