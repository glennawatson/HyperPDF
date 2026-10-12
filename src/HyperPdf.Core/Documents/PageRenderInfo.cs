// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Documents;

/// <summary>Describes which part of a page to render and how.</summary>
/// <param name="PageIndex">The zero based page index.</param>
/// <param name="Scale">Device pixels per page point.</param>
/// <param name="Rotation">The page rotation.</param>
/// <param name="OffsetX">The horizontal offset, in device pixels, of the target's origin within the full rotated page image.</param>
/// <param name="OffsetY">The vertical offset, in device pixels, of the target's origin within the full rotated page image.</param>
/// <param name="Flags">The render options.</param>
[DebuggerDisplay("PageRenderInfo: Page {PageIndex} x{Scale}")]
public readonly record struct PageRenderInfo(int PageIndex, float Scale, PageRotation Rotation, int OffsetX, int OffsetY, RenderFlags Flags);
