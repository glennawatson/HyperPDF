// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Rendering;

/// <summary>Identifies one decoded resolution of an image stream.</summary>
/// <param name="Stream">The source image.</param>
/// <param name="ReductionLevels">The finest wavelet levels omitted, or zero for full size.</param>
internal readonly record struct ImageKey(PdfStream Stream, int ReductionLevels);
