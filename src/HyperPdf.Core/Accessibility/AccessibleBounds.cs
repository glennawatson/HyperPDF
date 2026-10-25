// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Accessibility;

/// <summary>Where an element is, in whole pixels from the top left of the screen.</summary>
/// <param name="X">The left edge.</param>
/// <param name="Y">The top edge.</param>
/// <param name="Width">The width.</param>
/// <param name="Height">The height.</param>
[DebuggerDisplay("AccessibleBounds: {X}, {Y}, {Width} x {Height}")]
public readonly record struct AccessibleBounds(int X, int Y, int Width, int Height);
