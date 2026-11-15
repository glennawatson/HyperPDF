// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Rendering.Vulkan;

/// <summary>The kinds of drawing a frame records.</summary>
internal enum VulkanDrawKind
{
    /// <summary>Clears the whole frame to a colour.</summary>
    Clear = 0,

    /// <summary>Fills a rectangle with a colour.</summary>
    Fill = 1,

    /// <summary>Draws a tile.</summary>
    Tile = 2,
}
