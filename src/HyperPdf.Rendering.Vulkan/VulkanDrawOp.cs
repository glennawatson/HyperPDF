// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>One drawing operation of a frame, kept until the frame ends so every upload happens before the render pass starts.</summary>
/// <param name="Kind">What to draw.</param>
/// <param name="Tile">The tile, for a tile draw.</param>
/// <param name="X">The left edge in pixels.</param>
/// <param name="Y">The top edge in pixels.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="Color">The colour, for a clear or fill.</param>
/// <param name="Smooth">Whether a tile is sampled smoothly.</param>
[DebuggerDisplay("VulkanDrawOp {Kind}")]
internal readonly record struct VulkanDrawOp(VulkanDrawKind Kind, VulkanTileSurface? Tile, float X, float Y, float Width, float Height, VulkanColor Color, bool Smooth);
