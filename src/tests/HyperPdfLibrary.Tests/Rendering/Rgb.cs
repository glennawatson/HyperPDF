// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>A colour to compare pixels against.</summary>
/// <param name="Red">The red channel.</param>
/// <param name="Green">The green channel.</param>
/// <param name="Blue">The blue channel.</param>
[DebuggerDisplay("Rgb: {Red}, {Green}, {Blue}")]
internal readonly record struct Rgb(int Red, int Green, int Blue)
{
    /// <summary>The largest channel value.</summary>
    private const int Max = 255;

    /// <summary>Gets white.</summary>
    internal static Rgb White => new(Max, Max, Max);

    /// <summary>Gets black.</summary>
    internal static Rgb Black => new(0, 0, 0);

    /// <summary>Gets red.</summary>
    internal static Rgb Red255 => new(Max, 0, 0);

    /// <summary>Gets green.</summary>
    internal static Rgb Green255 => new(0, Max, 0);

    /// <summary>Gets blue.</summary>
    internal static Rgb Blue255 => new(0, 0, Max);

    /// <summary>Gets yellow.</summary>
    internal static Rgb Yellow => new(Max, Max, 0);
}
