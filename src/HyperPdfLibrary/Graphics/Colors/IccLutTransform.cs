// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers.Binary;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.Intrinsics;

namespace HyperPdfLibrary.Graphics.Colors;

/// <summary>
/// Converts the colours of a look-up table ICC profile (CMYK, Lab, wide RGB or any 1 to 8 channel device) to sRGB. The
/// profile's pipeline runs once per grid node to build a table of sRGB values; pixels then interpolate in that table, with
/// no allocation. The table is built on first use and shared by every thread.
/// </summary>
[DebuggerDisplay("IccLutTransform: {Components} components, {_rendering}")]
internal sealed class IccLutTransform : IccTransform
{
    /// <summary>The channels of an RGB grid.</summary>
    private const int RgbChannels = 3;

    /// <summary>The bytes in a CMYK pixel.</summary>
    private const int CmykBytes = 4;

    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The start of the second channel's row table.</summary>
    private const int SecondChannel = PixelConverter.LookupSize;

    /// <summary>The start of the third channel's row table.</summary>
    private const int ThirdChannel = SecondChannel + PixelConverter.LookupSize;

    /// <summary>The start of the fourth channel's row table.</summary>
    private const int FourthChannel = ThirdChannel + PixelConverter.LookupSize;

    /// <summary>The largest L* of a Lab device colour.</summary>
    private const float LabLightnessRange = 100F;

    /// <summary>The range of a Lab device colour's a* and b*.</summary>
    private const float LabChromaRange = 255F;

    /// <summary>The offset of a Lab device colour's a* and b*.</summary>
    private const float LabChromaOffset = 128F;

    /// <summary>The smallest a* or b* of a Lab device colour.</summary>
    private const float LabChromaMinimum = -128F;

    /// <summary>The largest a* or b* of a Lab device colour.</summary>
    private const float LabChromaMaximum = 127F;

    /// <summary>The rounding bias that makes a truncating conversion round to nearest.</summary>
    private const float RoundHalf = 0.5F;

    /// <summary>The profile this transform belongs to.</summary>
    private readonly IccLutProfile _profile;

    /// <summary>The pipeline from device values to D50 XYZ.</summary>
    private readonly IccPipeline _pipeline;

    /// <summary>The map that moves the profile's black point to the sRGB black point, or <see langword="null"/>.</summary>
    private readonly BlackPointCompensation? _compensation;

    /// <summary>The rendering this transform serves.</summary>
    private readonly IccRendering _rendering;

    /// <summary>Guards building the grid.</summary>
    private readonly Lock _gate = new();

    /// <summary>The grid and its row tables, or <see langword="null"/> before the first conversion.</summary>
    private IccGrid? _grid;

    /// <summary>Initializes a new instance of the <see cref="IccLutTransform"/> class.</summary>
    /// <param name="profile">The profile.</param>
    /// <param name="pipeline">The pipeline of the intent's tag.</param>
    /// <param name="compensation">The black point compensation, or <see langword="null"/>.</param>
    /// <param name="rendering">The rendering intent and black point choice.</param>
    internal IccLutTransform(IccLutProfile profile, IccPipeline pipeline, BlackPointCompensation? compensation, IccRendering rendering)
    {
        _profile = profile;
        _pipeline = pipeline;
        _compensation = compensation;
        _rendering = rendering;
    }

    /// <inheritdoc/>
    internal override int Components => _pipeline.Inputs;

    /// <inheritdoc/>
    internal override bool IsSrgb => _pipeline.Inputs == RgbChannels && !_profile.HasLabInput && GetGrid().IsIdentity;

    /// <inheritdoc/>
    internal override float[]? DefaultRange => _profile.HasLabInput
        ? [0F, LabLightnessRange, LabChromaMinimum, LabChromaMaximum, LabChromaMinimum, LabChromaMaximum]
        : null;

    /// <inheritdoc/>
    internal override IccTransform ForRendering(IccRendering rendering) => rendering == _rendering ? this : _profile.Get(rendering);

    /// <inheritdoc/>
    internal override void ToRgb(ReadOnlySpan<float> components, Span<float> rgb)
    {
        var clut = GetGrid().Clut;
        Span<int> offsets = stackalloc int[IccClut.MaxInputs + RgbChannels];
        Span<float> fractions = stackalloc float[IccClut.MaxInputs + RgbChannels];
        offsets = offsets[..clut.Dimensions];
        fractions = fractions[..clut.Dimensions];
        var inputs = _pipeline.Inputs;
        for (var d = 0; d < inputs; d++)
        {
            clut.Locate(d, Encode(components[d], d), out offsets[d], out fractions[d]);
        }

        var result = Vector128.Clamp(clut.Interpolate(offsets, fractions), Vector128<float>.Zero, Vector128<float>.One);
        rgb[0] = result.GetElement(0);
        rgb[1] = result.GetElement(1);
        rgb[RgbChannels - 1] = result.GetElement(RgbChannels - 1);
    }

    /// <inheritdoc/>
    internal override void ConvertRow(ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount)
    {
        var grid = GetGrid();
        var clut = grid.Clut;
        var inputs = _pipeline.Inputs;
        if (inputs == CmykBytes)
        {
            ConvertCmykRow(grid, samples, bgra, pixelCount);
            return;
        }

        if (inputs == RgbChannels)
        {
            ConvertRgbRow(grid, samples, bgra, pixelCount);
            return;
        }

        Span<int> offsets = stackalloc int[IccClut.MaxInputs + RgbChannels];
        Span<float> fractions = stackalloc float[IccClut.MaxInputs + RgbChannels];
        offsets = offsets[..clut.Dimensions];
        fractions = fractions[..clut.Dimensions];
        offsets.Clear();
        fractions.Clear();
        var previous = 0UL;
        var word = 0U;
        for (var p = 0; p < pixelCount; p++)
        {
            var pixel = samples.Slice(p * inputs, inputs);
            var key = Key(pixel);
            if (p == 0 || key != previous)
            {
                word = ConvertPixel(grid, pixel, offsets, fractions);
                previous = key;
            }

            BinaryPrimitives.WriteUInt32LittleEndian(bgra[(p * PixelConverter.BytesPerPixel)..], word);
        }
    }

    /// <summary>Converts a row of four-channel samples, with no loops over channels and a reused result for equal neighbours.</summary>
    /// <param name="grid">The grid.</param>
    /// <param name="samples">The samples.</param>
    /// <param name="bgra">Receives four bytes per pixel.</param>
    /// <param name="pixelCount">The number of pixels.</param>
    private static void ConvertCmykRow(IccGrid grid, ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount)
    {
        var offsets = grid.Offsets;
        var fractions = grid.Fractions;
        var clut = grid.Clut;
        var previous = 0U;
        var word = 0U;
        for (var p = 0; p < pixelCount; p++)
        {
            var key = BinaryPrimitives.ReadUInt32LittleEndian(samples[(p * CmykBytes)..]);
            if (p == 0 || key != previous)
            {
                var c = (int)(key & PixelConverter.MaxByte);
                var m = (int)((key >> ByteBits) & PixelConverter.MaxByte);
                var y = (int)((key >> (ByteBits + ByteBits)) & PixelConverter.MaxByte);
                var k = (int)(key >> (ByteBits + ByteBits + ByteBits));
                var node = offsets[c] + offsets[SecondChannel + m] + offsets[ThirdChannel + y] + offsets[FourthChannel + k];
                word = Pack(clut.Interpolate4(node, fractions[c], fractions[m], fractions[y], fractions[k]));
                previous = key;
            }

            BinaryPrimitives.WriteUInt32LittleEndian(bgra[(p * PixelConverter.BytesPerPixel)..], word);
        }
    }

    /// <summary>Converts a row of three-channel samples, with a reused result for equal neighbours.</summary>
    /// <param name="grid">The grid.</param>
    /// <param name="samples">The samples.</param>
    /// <param name="bgra">Receives four bytes per pixel.</param>
    /// <param name="pixelCount">The number of pixels.</param>
    private static void ConvertRgbRow(IccGrid grid, ReadOnlySpan<byte> samples, Span<byte> bgra, int pixelCount)
    {
        var offsets = grid.Offsets;
        var fractions = grid.Fractions;
        var clut = grid.Clut;
        var previous = 0U;
        var word = 0U;
        for (var p = 0; p < pixelCount; p++)
        {
            var pixel = samples.Slice(p * RgbChannels, RgbChannels);
            var key = (uint)(pixel[0] | (pixel[1] << ByteBits) | (pixel[RgbChannels - 1] << (ByteBits + ByteBits)));
            if (p == 0 || key != previous)
            {
                var a = (int)pixel[0];
                var b = (int)pixel[1];
                var c = (int)pixel[RgbChannels - 1];
                var node = offsets[a] + offsets[SecondChannel + b] + offsets[ThirdChannel + c];
                word = Pack(clut.Interpolate3(node, fractions[a], fractions[b], fractions[c]));
                previous = key;
            }

            BinaryPrimitives.WriteUInt32LittleEndian(bgra[(p * PixelConverter.BytesPerPixel)..], word);
        }
    }

    /// <summary>Packs up to eight sample bytes into one number so that equal pixels compare in one step.</summary>
    /// <param name="pixel">The sample bytes.</param>
    /// <returns>The packed key.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static ulong Key(ReadOnlySpan<byte> pixel)
    {
        if (pixel.Length == CmykBytes)
        {
            return BinaryPrimitives.ReadUInt32LittleEndian(pixel);
        }

        var key = 0UL;
        for (var i = 0; i < pixel.Length; i++)
        {
            key |= (ulong)pixel[i] << (i * ByteBits);
        }

        return key;
    }

    /// <summary>Packs a table colour into an opaque BGRA word.</summary>
    /// <param name="color">The colour in the first three lanes, from 0 to 1; values outside are clipped.</param>
    /// <returns>The word, to be written little-endian.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint Pack(Vector128<float> color)
    {
        var scaled = Vector128.ConvertToInt32((Vector128.Clamp(color, Vector128<float>.Zero, Vector128<float>.One) * PixelConverter.MaxByte) + Vector128.Create(RoundHalf));
        return PixelConverter.Pack((byte)scaled.GetElement(0), (byte)scaled.GetElement(1), (byte)scaled.GetElement(RgbChannels - 1));
    }

    /// <summary>Converts one pixel through the grid.</summary>
    /// <param name="grid">The grid.</param>
    /// <param name="pixel">The sample bytes.</param>
    /// <param name="offsets">Scratch for the cell offsets; the padding dimensions stay zero.</param>
    /// <param name="fractions">Scratch for the cell fractions; the padding dimensions stay zero.</param>
    /// <returns>The BGRA word.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static uint ConvertPixel(IccGrid grid, ReadOnlySpan<byte> pixel, Span<int> offsets, Span<float> fractions)
    {
        for (var c = 0; c < pixel.Length; c++)
        {
            var sample = pixel[c];
            offsets[c] = grid.Offsets[(c * PixelConverter.LookupSize) + sample];
            fractions[c] = grid.Fractions[sample];
        }

        return Pack(grid.Clut.Interpolate(offsets, fractions));
    }

    /// <summary>Converts a component, which is in Lab units for a Lab profile, to its 0..1 position in the table.</summary>
    /// <param name="value">The component.</param>
    /// <param name="channel">The channel index.</param>
    /// <returns>The position in the table, before clamping.</returns>
    private float Encode(float value, int channel)
    {
        if (!_profile.HasLabInput)
        {
            return value;
        }

        return channel == 0 ? value / LabLightnessRange : (value + LabChromaOffset) / LabChromaRange;
    }

    /// <summary>Gets the grid, building it on first use.</summary>
    /// <returns>The grid.</returns>
    private IccGrid GetGrid() => Volatile.Read(ref _grid) ?? BuildGrid();

    /// <summary>Builds the grid.</summary>
    /// <returns>The grid.</returns>
    private IccGrid BuildGrid()
    {
        lock (_gate)
        {
            if (_grid is { } existing)
            {
                return existing;
            }

            var built = IccGrid.Build(_pipeline, _compensation, _profile.HasLabInput);
            Volatile.Write(ref _grid, built);
            return built;
        }
    }
}
