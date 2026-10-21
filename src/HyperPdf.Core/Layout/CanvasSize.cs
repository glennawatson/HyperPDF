// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Layout;

/// <summary>A size on the canvas, in canvas units.</summary>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
[DebuggerDisplay("CanvasSize: {Width} x {Height}")]
public readonly record struct CanvasSize(double Width, double Height);
