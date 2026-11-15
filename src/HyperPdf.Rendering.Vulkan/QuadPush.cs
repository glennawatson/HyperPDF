// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>The push constants of one quad: where it goes, how big the frame is and, for a fill, its colour.</summary>
/// <param name="X">The left edge in pixels.</param>
/// <param name="Y">The top edge in pixels.</param>
/// <param name="Width">The width in pixels.</param>
/// <param name="Height">The height in pixels.</param>
/// <param name="ViewportWidth">The frame width in pixels.</param>
/// <param name="ViewportHeight">The frame height in pixels.</param>
/// <param name="Color">The premultiplied fill colour, unused by tiles.</param>
[DebuggerDisplay("QuadPush ({X}, {Y}) {Width} x {Height}")]
internal readonly record struct QuadPush(float X, float Y, float Width, float Height, float ViewportWidth, float ViewportHeight, VulkanColor Color);
