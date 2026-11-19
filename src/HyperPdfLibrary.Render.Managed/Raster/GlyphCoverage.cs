// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Render.Managed.Raster;

/// <summary>Where the kept coverage of one glyph is and which pixels around the glyph's origin it covers.</summary>
/// <param name="Offset">Where its first byte is in the bytes of the cache.</param>
/// <param name="Left">The first pixel across it covers, counted from the pixel of the glyph's origin.</param>
/// <param name="Top">The first line it covers, counted from the pixel of the glyph's origin.</param>
/// <param name="Width">The pixels across it covers, 0 for a glyph that covers nothing.</param>
/// <param name="Height">The lines it covers.</param>
[DebuggerDisplay("GlyphCoverage: {Width} x {Height} at {Left},{Top}")]
internal readonly record struct GlyphCoverage(int Offset, int Left, int Top, int Width, int Height);
