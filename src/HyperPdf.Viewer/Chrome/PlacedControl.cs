// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Layout;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Where a row puts its next control, worked out before the control is made.</summary>
/// <param name="Bounds">Where the control goes, in device pixels.</param>
/// <param name="HidesText">Whether the control shows its icon alone.</param>
[DebuggerDisplay("PlacedControl: {Bounds}")]
internal readonly record struct PlacedControl(LayoutRect Bounds, bool HidesText);
