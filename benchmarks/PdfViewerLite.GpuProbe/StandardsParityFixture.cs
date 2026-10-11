// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Rendering;
using PdfViewerLite.HyperPdf.Tests.RenderingComparison;

namespace PdfViewerLite.GpuProbe;

/// <summary>A synthetic standards PDF and its expected render flags.</summary>
/// <param name="Name">The graphics family.</param>
/// <param name="Pdf">The generated PDF bytes.</param>
/// <param name="Width">The expected pixel width.</param>
/// <param name="Height">The expected pixel height.</param>
/// <param name="Flags">The rendering flags.</param>
/// <param name="SimulateOverprint">Whether to enable overprint simulation.</param>
/// <param name="Oracle">Optional independently derived interior colours.</param>
internal readonly record struct StandardsParityFixture(string Name, byte[] Pdf, int Width, int Height, PdfRenderFlags Flags, bool SimulateOverprint, StandardsOracle? Oracle);
