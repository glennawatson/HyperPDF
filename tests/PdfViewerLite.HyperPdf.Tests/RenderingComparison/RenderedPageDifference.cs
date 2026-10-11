// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.HyperPdf.Tests.RenderingComparison;

/// <summary>Reports raster differences without treating agreement as a standards oracle.</summary>
/// <param name="Score">The raw per-channel and per-pixel difference counts.</param>
/// <param name="MeanAbsoluteChannelError">The mean channel error, or null when dimensions differ.</param>
internal sealed record RenderedPageDifference(RasterScore Score, double? MeanAbsoluteChannelError);
