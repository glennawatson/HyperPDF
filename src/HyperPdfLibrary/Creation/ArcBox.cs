// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Creation;

/// <summary>The ellipse an arc follows: its centre and its two radii.</summary>
/// <param name="CenterX">The centre's x.</param>
/// <param name="CenterY">The centre's y.</param>
/// <param name="RadiusX">The horizontal radius.</param>
/// <param name="RadiusY">The vertical radius.</param>
[DebuggerDisplay("ArcBox: ({CenterX}, {CenterY}) radii {RadiusX} x {RadiusY}")]
internal readonly record struct ArcBox(float CenterX, float CenterY, float RadiusX, float RadiusY);
