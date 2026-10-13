// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>How big a push button's icon is drawn and where it sits in its space.</summary>
/// <param name="ScaleX">The horizontal scale.</param>
/// <param name="ScaleY">The vertical scale.</param>
/// <param name="OffsetX">The distance from the space's left edge to the icon's left edge.</param>
/// <param name="OffsetY">The distance from the space's lower edge to the icon's lower edge.</param>
[DebuggerDisplay("FormIconPlacement: {ScaleX} x {ScaleY} at {OffsetX}, {OffsetY}")]
internal readonly record struct FormIconPlacement(float ScaleX, float ScaleY, float OffsetX, float OffsetY);
