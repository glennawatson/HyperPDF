// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdfLibrary.Text;

namespace HyperPdf.Benchmarks;

/// <summary>The original character records and focused query used by both search paths.</summary>
/// <param name="Characters">The original records.</param>
/// <param name="Point">The point in user space.</param>
/// <param name="Tolerance">The same tolerance on both axes.</param>
internal sealed record HyperPdfHitFixture(PdfTextChar[] Characters, Vector2 Point, float Tolerance);
