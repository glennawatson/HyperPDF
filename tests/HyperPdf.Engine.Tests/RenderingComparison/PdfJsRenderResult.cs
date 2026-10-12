// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Pairs the owned browser raster with its actual renderer identity and geometry.</summary>
/// <param name="Raster">The white-background BGRA32 snapshot.</param>
/// <param name="Metadata">The actual renderer identity and geometry.</param>
internal sealed record PdfJsRenderResult(ComparisonRaster Raster, PdfJsRendererMetadata Metadata);
