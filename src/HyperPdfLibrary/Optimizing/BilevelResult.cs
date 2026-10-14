// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>A black-and-white image re-encoded smaller than it was.</summary>
/// <param name="Stream">The new image.</param>
/// <param name="Coding">A description of the coding that won, for the report.</param>
/// <param name="IsLossless">Whether every pixel is kept.</param>
[DebuggerDisplay("BilevelResult: {Coding}")]
internal sealed record BilevelResult(PdfStream Stream, string Coding, bool IsLossless);
