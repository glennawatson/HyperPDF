// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.ImageFiles;

/// <summary>What is needed to paint one row of a GIF frame on the screen.</summary>
/// <param name="Screen">The screen.</param>
/// <param name="Table">The colour table: red, green and blue bytes for each entry.</param>
/// <param name="Frame">Where the frame sits on the screen.</param>
/// <param name="Y">The screen row the stored row belongs to.</param>
[DebuggerDisplay("GifRowPlace: row {Y}")]
internal readonly record struct GifRowPlace(GifScreen Screen, byte[] Table, GifFrame Frame, int Y);
