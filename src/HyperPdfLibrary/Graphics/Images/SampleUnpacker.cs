// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Graphics.Colors;

namespace HyperPdfLibrary.Graphics.Images;

/// <summary>
/// Unpacks rows of 1, 2, 4, 8 or 16-bit image samples into the 8-bit samples <see cref="PdfColorSpace.ConvertRow"/>
/// takes. The image's /Decode array is folded into one 256-entry table per component, built once per image; 16-bit
/// samples are rounded to 8 bits first. Colour-key masking compares the raw samples. An 8-bit image that /Decode leaves
/// unchanged shares one static table, so it builds nothing.
/// </summary>
internal readonly record struct SampleUnpacker
{
    /// <summary>The bits in a byte.</summary>
    private const int ByteBits = 8;

    /// <summary>The bits of a 16-bit sample.</summary>
    private const int WideBits = 16;

    /// <summary>The bytes of a 16-bit sample.</summary>
    private const int WideBytes = 2;

    /// <summary>The largest 16-bit sample.</summary>
    private const int MaxWide = ushort.MaxValue;

    /// <summary>Half of <see cref="MaxWide"/>, for rounding.</summary>
    private const int HalfWide = MaxWide / 2;

    /// <summary>The bits per sample.</summary>
    private readonly int _bits;

    /// <summary>The components per pixel.</summary>
    private readonly int _components;

    /// <summary>The 8-bit output for each component and reduced raw value, 256 entries per component.</summary>
    private readonly byte[] _lookup;

    /// <summary>The colour-key ranges in raw sample values, or <see langword="null"/>.</summary>
    private readonly int[]? _colorKey;

    /// <summary>Initializes a new instance of the <see cref="SampleUnpacker"/> class.</summary>
    /// <param name="bits">The bits per sample.</param>
    /// <param name="components">The components per pixel.</param>
    /// <param name="decode">The image's decode ranges.</param>
    /// <param name="space">The colour space whose 8-bit default decode the output is scaled to.</param>
    /// <param name="colorKey">The colour-key ranges, or <see langword="null"/>.</param>
    internal SampleUnpacker(int bits, int components, float[] decode, PdfColorSpace space, int[]? colorKey)
    {
        _bits = bits;
        _components = components;
        _colorKey = colorKey;
        var target = space.ByteDecode;
        if (bits == ByteBits && components <= SampleLookup.SharedComponents && SampleLookup.IsIdentity(decode, target, components))
        {
            _lookup = SampleLookup.Identity;
            IsIdentity = true;
            return;
        }

        _lookup = new byte[components * PixelConverter.LookupSize];
        IsIdentity = SampleLookup.Build(_lookup, bits, components, decode, target);
    }

    /// <summary>Gets a value indicating whether 8-bit rows need no unpacking and no colour-key test.</summary>
    internal bool IsDirect => _bits == ByteBits && IsIdentity && _colorKey is null;

    /// <summary>Gets a value indicating whether a colour key is applied.</summary>
    internal bool HasColorKey => _colorKey is not null;

    /// <summary>Gets a value indicating whether the table maps every byte to itself.</summary>
    private bool IsIdentity { get; }

    /// <summary>Maps an 8-bit sample of a component through the table.</summary>
    /// <param name="component">The component.</param>
    /// <param name="value">The sample.</param>
    /// <returns>The mapped sample.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal byte Map(int component, byte value) => _lookup[(component * PixelConverter.LookupSize) + value];

    /// <summary>Unpacks one row.</summary>
    /// <param name="row">The packed samples.</param>
    /// <param name="samples">Receives one byte per component.</param>
    /// <param name="alpha">Receives 0 for colour-keyed pixels and 255 for others; ignored without a colour key.</param>
    /// <param name="width">The pixels in the row.</param>
    internal void Unpack(ReadOnlySpan<byte> row, Span<byte> samples, Span<byte> alpha, int width)
    {
        if (_colorKey is not null)
        {
            UnpackKeyed(row, samples, alpha, width);
            return;
        }

        if (_bits == ByteBits)
        {
            UnpackBytes(row, samples, width);
            return;
        }

        UnpackGeneric(row, samples, width);
    }

    /// <summary>Reads one raw sample.</summary>
    /// <param name="row">The packed row.</param>
    /// <param name="index">The sample index in the row.</param>
    /// <returns>The raw value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int ReadRaw(ReadOnlySpan<byte> row, int index)
    {
        switch (_bits)
        {
            case ByteBits:
            {
                return row[index];
            }

            case WideBits:
            {
                var pair = row.Slice(index * WideBytes, WideBytes);
                return (pair[0] << ByteBits) | pair[1];
            }

            default:
            {
                var bit = index * _bits;
                return (row[bit / ByteBits] >> (ByteBits - _bits - (bit % ByteBits))) & ((1 << _bits) - 1);
            }
        }
    }

    /// <summary>Reduces a raw sample to the table's index range.</summary>
    /// <param name="raw">The raw sample.</param>
    /// <returns>The index.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int Reduce(int raw) => _bits == WideBits ? ((raw * PixelConverter.MaxByte) + HalfWide) / MaxWide : raw;

    /// <summary>Unpacks 8-bit samples through the table.</summary>
    /// <param name="row">The packed row.</param>
    /// <param name="samples">Receives the samples.</param>
    /// <param name="width">The pixels in the row.</param>
    private void UnpackBytes(ReadOnlySpan<byte> row, Span<byte> samples, int width)
    {
        var count = width * _components;
        for (var c = 0; c < _components; c++)
        {
            var table = _lookup.AsSpan(c * PixelConverter.LookupSize, PixelConverter.LookupSize);
            for (var i = c; i < count; i += _components)
            {
                samples[i] = table[row[i]];
            }
        }
    }

    /// <summary>Unpacks samples of any depth through the table.</summary>
    /// <param name="row">The packed row.</param>
    /// <param name="samples">Receives the samples.</param>
    /// <param name="width">The pixels in the row.</param>
    private void UnpackGeneric(ReadOnlySpan<byte> row, Span<byte> samples, int width)
    {
        var count = width * _components;
        for (var i = 0; i < count; i++)
        {
            samples[i] = _lookup[((i % _components) * PixelConverter.LookupSize) + Reduce(ReadRaw(row, i))];
        }
    }

    /// <summary>Unpacks samples and tests each pixel against the colour key.</summary>
    /// <param name="row">The packed row.</param>
    /// <param name="samples">Receives the samples.</param>
    /// <param name="alpha">Receives the per-pixel alpha.</param>
    /// <param name="width">The pixels in the row.</param>
    private void UnpackKeyed(ReadOnlySpan<byte> row, Span<byte> samples, Span<byte> alpha, int width)
    {
        var key = _colorKey!;
        for (var p = 0; p < width; p++)
        {
            var keyed = true;
            for (var c = 0; c < _components; c++)
            {
                var index = (p * _components) + c;
                var raw = ReadRaw(row, index);
                keyed &= raw >= key[PdfColorSpace.PairSize * c] && raw <= key[(PdfColorSpace.PairSize * c) + 1];
                samples[index] = _lookup[(c * PixelConverter.LookupSize) + Reduce(raw)];
            }

            alpha[p] = keyed ? (byte)0 : (byte)PixelConverter.MaxByte;
        }
    }
}
