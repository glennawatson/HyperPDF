// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Windowing.Rendering;

/// <summary>A run of a frame's vertices drawn with one texture and sampler, or a clear of the whole frame.</summary>
/// <typeparam name="TTile">The backend's tile type.</typeparam>
/// <param name="IsClear">Whether this clears the frame to <paramref name="Color"/> instead of drawing vertices.</param>
/// <param name="Tile">The tile the vertices sample, or <see langword="null"/> for solid fills.</param>
/// <param name="Smooth">Whether the tile is sampled smoothly.</param>
/// <param name="First">The first vertex.</param>
/// <param name="Count">The number of vertices.</param>
/// <param name="Color">The colour of a clear as 0xAARRGGBB.</param>
[DebuggerDisplay("QuadBatch clear {IsClear}, {Count} vertices from {First}")]
public readonly record struct QuadBatch<TTile>(bool IsClear, TTile? Tile, bool Smooth, int First, int Count, uint Color)
    where TTile : class;
