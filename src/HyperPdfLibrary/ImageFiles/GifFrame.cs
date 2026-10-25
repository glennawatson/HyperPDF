// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>Where a GIF frame sits on the screen and how its rows are stored.</summary>
/// <param name="Left">The frame's left edge.</param>
/// <param name="Top">The frame's top edge.</param>
/// <param name="Width">The frame's width.</param>
/// <param name="Height">The frame's height.</param>
/// <param name="Interlaced">Whether the rows are stored in four interlace passes.</param>
[DebuggerDisplay("GifFrame: {Width}x{Height} at ({Left}, {Top})")]
internal readonly record struct GifFrame(int Left, int Top, int Width, int Height, bool Interlaced);
