// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Rendering;

namespace HyperPdf.Viewer.Text;

/// <summary>A piece of text drawn once into a surface, ready to be placed in any frame.</summary>
/// <param name="Surface">The surface holding the text, or <see langword="null"/> for text with nothing to draw.</param>
/// <param name="Width">The width in device pixels.</param>
/// <param name="Height">The height in device pixels.</param>
[DebuggerDisplay("TextRun: {Width} x {Height}")]
public sealed record TextRun(IRenderSurface? Surface, int Width, int Height);
