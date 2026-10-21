// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Geometry;

namespace HyperPdf.Core.Layout;

/// <summary>Inputs to <see cref="ZoomCalculator"/>.</summary>
/// <param name="Rotation">The page rotation.</param>
/// <param name="Mode">The page arrangement.</param>
/// <param name="ZoomMode">The fit mode.</param>
/// <param name="ViewportWidth">The viewport width.</param>
/// <param name="ViewportHeight">The viewport height.</param>
/// <param name="Spacing">The gap between pages.</param>
/// <param name="Margin">The content margin.</param>
[DebuggerDisplay("FitParameters: {ZoomMode} {ViewportWidth} x {ViewportHeight}")]
public readonly record struct FitParameters(PageRotation Rotation, PageLayoutMode Mode, ZoomMode ZoomMode, double ViewportWidth, double ViewportHeight, double Spacing, double Margin);
