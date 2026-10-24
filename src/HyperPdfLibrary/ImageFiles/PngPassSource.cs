// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>The filtered rows of a PNG file and the samples they are unpacked into.</summary>
/// <param name="Raw">The filtered rows of every pass.</param>
/// <param name="Offset">Where the current pass starts in the filtered rows.</param>
/// <param name="Header">The header.</param>
/// <param name="Samples">The samples being filled, pixel by pixel in the picture's size.</param>
[DebuggerDisplay("PngPassSource: offset {Offset}")]
internal readonly record struct PngPassSource(byte[] Raw, int Offset, PngHeader Header, ushort[] Samples);
