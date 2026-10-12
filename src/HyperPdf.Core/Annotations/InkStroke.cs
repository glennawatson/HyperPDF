// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Annotations;

/// <summary>A freehand drawing: one or more strokes, stored as a flat point list with the length of each stroke.</summary>
/// <param name="Points">Every point, in page space, stroke after stroke.</param>
/// <param name="StrokeLengths">The number of points in each stroke.</param>
[DebuggerDisplay("InkStroke: {StrokeLengths.Length} strokes")]
public sealed record InkStroke(PagePoint[] Points, int[] StrokeLengths);
