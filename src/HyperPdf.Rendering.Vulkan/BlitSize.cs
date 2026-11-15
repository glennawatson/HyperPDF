// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Rendering.Vulkan;

/// <summary>The sizes of the two images of a whole image copy.</summary>
/// <param name="SourceWidth">The source width in pixels.</param>
/// <param name="SourceHeight">The source height in pixels.</param>
/// <param name="TargetWidth">The destination width in pixels.</param>
/// <param name="TargetHeight">The destination height in pixels.</param>
[DebuggerDisplay("BlitSize {SourceWidth} x {SourceHeight} to {TargetWidth} x {TargetHeight}")]
internal readonly record struct BlitSize(int SourceWidth, int SourceHeight, int TargetWidth, int TargetHeight);
