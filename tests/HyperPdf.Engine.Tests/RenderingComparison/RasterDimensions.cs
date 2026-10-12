// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>The actual raster geometry returned by one renderer.</summary>
/// <param name="Width">The raster width.</param>
/// <param name="Height">The raster height.</param>
internal readonly record struct RasterDimensions(int Width, int Height);
