// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using Avalonia.Media;
using HarfBuzzSharp;
using SkiaSharp;

namespace HyperPdf.Skia.Fonts;

/// <summary>Owns a HarfBuzz font backed by Skia's font bytes.</summary>
internal sealed class ShapingTypeface : ITextShaperTypeface
{
    /// <summary>Initializes a new instance of the <see cref="ShapingTypeface"/> class.</summary>
    /// <param name="typeface">The borrowed platform typeface.</param>
    public ShapingTypeface(SkiaTypeface typeface)
    {
        Typeface = typeface;
        var stream = typeface.Typeface.OpenStream(out var index);
        var address = stream.GetMemoryBase();
        using var blob = CreateBlob(stream, address);
        using var face = new Face(blob, index);
        UnitsPerEm = face.UnitsPerEm;
        Font = new(face);
        Font.SetFunctionsOpenType();
        Font.SetScale(UnitsPerEm, UnitsPerEm);
    }

    /// <summary>Gets the borrowed platform typeface.</summary>
    internal SkiaTypeface Typeface { get; }

    /// <summary>Gets the cached OpenType font.</summary>
    internal Font Font { get; }

    /// <summary>Gets the font's design units per em.</summary>
    internal int UnitsPerEm { get; }

    /// <inheritdoc />
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => Font.Dispose();

    /// <summary>Retains the font stream without copying when its bytes are addressable.</summary>
    /// <param name="stream">The owned font stream.</param>
    /// <param name="address">The stream's memory address.</param>
    /// <returns>The owned font blob.</returns>
    private static Blob CreateBlob(SKStreamAsset stream, IntPtr address)
    {
        if (address != IntPtr.Zero)
        {
            return new(address, stream.Length, MemoryMode.ReadOnly, stream.Dispose);
        }

        using (stream)
        {
            var data = SKData.Create(stream);
            return new(data.Data, (int)data.Size, MemoryMode.ReadOnly, data.Dispose);
        }
    }
}
