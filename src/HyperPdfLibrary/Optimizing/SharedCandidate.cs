// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Optimizing;

/// <summary>A 1-bit image that may share a JBIG2 symbol dictionary with others.</summary>
/// <param name="Number">The image's object number.</param>
/// <param name="Stream">The image stream.</param>
/// <param name="Rows">The packed rows, in the layout of the image's samples.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
[DebuggerDisplay("SharedCandidate: object {Number}, {Width}x{Height}")]
internal sealed record SharedCandidate(int Number, PdfStream Stream, byte[] Rows, int Width, int Height);
