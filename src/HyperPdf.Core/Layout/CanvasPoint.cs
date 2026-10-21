// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Layout;

/// <summary>A point on the canvas, in canvas units.</summary>
/// <param name="X">The horizontal position.</param>
/// <param name="Y">The vertical position.</param>
[DebuggerDisplay("CanvasPoint: ({X}, {Y})")]
public readonly record struct CanvasPoint(double X, double Y);
