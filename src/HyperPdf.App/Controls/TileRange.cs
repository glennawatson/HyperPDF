// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.App.Controls;

/// <summary>The tile grid of one page at one scale.</summary>
/// <param name="Page">The page index.</param>
/// <param name="ScaleKey">The quantised scale.</param>
/// <param name="Scale">Device pixels per point.</param>
/// <param name="PixelWidth">The full page width in device pixels.</param>
/// <param name="PixelHeight">The full page height in device pixels.</param>
/// <param name="OriginX">The horizontal page origin on the device, snapped to whole pixels.</param>
/// <param name="OriginY">The vertical page origin on the device, snapped to whole pixels.</param>
internal readonly record struct TileRange(int Page, int ScaleKey, float Scale, int PixelWidth, int PixelHeight, double OriginX, double OriginY);
