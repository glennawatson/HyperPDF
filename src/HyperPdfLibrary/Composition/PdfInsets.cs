// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>Space inside a border, on each side, in points.</summary>
/// <param name="Left">The space on the left.</param>
/// <param name="Top">The space at the top.</param>
/// <param name="Right">The space on the right.</param>
/// <param name="Bottom">The space at the bottom.</param>
[DebuggerDisplay("PdfInsets: {Left}, {Top}, {Right}, {Bottom}")]
public readonly record struct PdfInsets(float Left, float Top, float Right, float Bottom)
{
    /// <summary>Gets no space.</summary>
    public static PdfInsets None => default;

    /// <summary>Gets the space on the left and right together.</summary>
    public float Horizontal => Left + Right;

    /// <summary>Gets the space at the top and bottom together.</summary>
    public float Vertical => Top + Bottom;

    /// <summary>Makes insets with the same space on every side.</summary>
    /// <param name="all">The space on each side.</param>
    /// <returns>The insets.</returns>
    public static PdfInsets Uniform(float all) => new(all, all, all, all);

    /// <summary>Makes insets from a horizontal and a vertical space.</summary>
    /// <param name="horizontal">The space on the left and on the right.</param>
    /// <param name="vertical">The space at the top and at the bottom.</param>
    /// <returns>The insets.</returns>
    public static PdfInsets Symmetric(float horizontal, float vertical) => new(horizontal, vertical, horizontal, vertical);
}
