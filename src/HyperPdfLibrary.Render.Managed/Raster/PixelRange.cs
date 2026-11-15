// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>A run of pixels on one line.</summary>
/// <param name="Start">The first pixel.</param>
/// <param name="End">One past the last pixel.</param>
[DebuggerDisplay("PixelRange: {Start} to {End}")]
internal readonly record struct PixelRange(int Start, int End)
{
    /// <summary>Gets a value indicating whether the run holds no pixel.</summary>
    internal bool IsEmpty => End <= Start;
}
