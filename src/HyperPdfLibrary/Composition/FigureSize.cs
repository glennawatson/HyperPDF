// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Composition;

/// <summary>The width and height of a figure in points.</summary>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
[DebuggerDisplay("FigureSize: {Width} x {Height}")]
internal readonly record struct FigureSize(float Width, float Height);
