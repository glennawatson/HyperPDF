// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>An RGB colour with component levels from 0 to 1.</summary>
/// <param name="Red">The red level from 0 to 1.</param>
/// <param name="Green">The green level from 0 to 1.</param>
/// <param name="Blue">The blue level from 0 to 1.</param>
[DebuggerDisplay("PdfRgbColor: rgb({Red}, {Green}, {Blue})")]
public readonly record struct PdfRgbColor(float Red, float Green, float Blue)
{
    /// <summary>Gets black (<c>0, 0, 0</c>).</summary>
    public static PdfRgbColor Black => new(0F, 0F, 0F);

    /// <summary>Gets white (<c>1, 1, 1</c>).</summary>
    public static PdfRgbColor White => new(1F, 1F, 1F);

    /// <summary>Gets a value indicating whether every channel is zero (black).</summary>
    internal bool IsBlack => Red == 0F && Green == 0F && Blue == 0F;
}
