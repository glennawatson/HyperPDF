// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>The parts of a PNG file the decoder needs.</summary>
/// <param name="Header">The header, or <see langword="null"/> when the file had none.</param>
/// <param name="Palette">The palette: red, green and blue bytes for each entry; empty when there is none.</param>
/// <param name="Transparency">The transparency chunk's data; empty when there is none.</param>
/// <param name="Data">The image data chunks, joined; still compressed.</param>
[DebuggerDisplay("PngParts: {Header}")]
internal readonly record struct PngParts(PngHeader? Header, byte[] Palette, byte[] Transparency, byte[] Data);
