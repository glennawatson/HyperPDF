// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Ocr.Tests;

/// <summary>A small greyscale page the layout tests draw boxes on.</summary>
internal sealed class GreyPage
{
    /// <summary>The paper level.</summary>
    private const byte Paper = 255;

    /// <summary>Initializes a new instance of the <see cref="GreyPage"/> class with a blank page.</summary>
    /// <param name="width">The width in pixels.</param>
    /// <param name="height">The height in pixels.</param>
    internal GreyPage(int width, int height)
    {
        Width = width;
        Height = height;
        Pixels = new byte[width * height];
        Array.Fill(Pixels, Paper);
    }

    /// <summary>Gets the width in pixels.</summary>
    internal int Width { get; }

    /// <summary>Gets the height in pixels.</summary>
    internal int Height { get; }

    /// <summary>Gets the pixels, row after row.</summary>
    internal byte[] Pixels { get; }

    /// <summary>Fills a box with black ink.</summary>
    /// <param name="region">The box.</param>
    /// <returns>This page.</returns>
    internal GreyPage Ink(Region region)
    {
        for (var y = region.Top; y <= region.Bottom; y++)
        {
            Pixels.AsSpan((y * Width) + region.Left, region.Width).Clear();
        }

        return this;
    }

    /// <summary>Swaps light and dark, so the ink is light on a dark page.</summary>
    /// <returns>This page.</returns>
    internal GreyPage Invert()
    {
        for (var i = 0; i < Pixels.Length; i++)
        {
            Pixels[i] = (byte)(byte.MaxValue - Pixels[i]);
        }

        return this;
    }
}
