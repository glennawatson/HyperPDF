// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Graphics.Images.Jpeg;

/// <summary>The size and block layout of a frame an encoder writes.</summary>
/// <param name="Width">The image width in pixels.</param>
/// <param name="Height">The image height in pixels.</param>
/// <param name="Components">The number of components.</param>
/// <param name="MaxHorizontal">The largest horizontal sampling factor.</param>
/// <param name="MaxVertical">The largest vertical sampling factor.</param>
/// <param name="McusPerLine">The MCUs across the image.</param>
/// <param name="McusPerColumn">The MCUs down the image.</param>
/// <param name="Kind">The colour model.</param>
internal readonly record struct JpegFrameGeometry(
    int Width,
    int Height,
    int Components,
    int MaxHorizontal,
    int MaxVertical,
    int McusPerLine,
    int McusPerColumn,
    JpegColorKind Kind);
