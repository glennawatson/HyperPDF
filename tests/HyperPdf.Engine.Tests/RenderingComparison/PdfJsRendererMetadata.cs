// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests.RenderingComparison;

/// <summary>Identifies the actual browser and page geometry used for a reference render.</summary>
/// <param name="RendererVersion">The pinned pdf.js version.</param>
/// <param name="BrowserVersion">The browser's negotiated version.</param>
/// <param name="BrowserBuild">The browser's negotiated build identifier.</param>
/// <param name="PageIndex">The zero-based rendered page.</param>
/// <param name="Scale">The requested pixels per PDF point.</param>
/// <param name="Rotation">The effective viewport rotation in degrees.</param>
/// <param name="ViewportWidth">The unrounded viewport width.</param>
/// <param name="ViewportHeight">The unrounded viewport height.</param>
/// <param name="DevicePixelRatio">The actual browser device pixel ratio.</param>
/// <param name="UserUnit">The PDF page's user unit multiplier.</param>
/// <param name="PageView">The actual PDF page view box.</param>
/// <param name="ViewportTransform">The actual PDF-to-canvas viewport transform.</param>
internal sealed record PdfJsRendererMetadata(
    string RendererVersion,
    string BrowserVersion,
    string BrowserBuild,
    int PageIndex,
    double Scale,
    int Rotation,
    double ViewportWidth,
    double ViewportHeight,
    double DevicePixelRatio,
    double UserUnit,
    ReadOnlyMemory<double> PageView,
    ReadOnlyMemory<double> ViewportTransform);
