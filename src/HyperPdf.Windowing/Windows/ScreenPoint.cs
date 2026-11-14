// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Windowing.Windows;

/// <summary>A place on the screen in device pixels.</summary>
/// <param name="X">Pixels from the left of the screen.</param>
/// <param name="Y">Pixels from the top of the screen.</param>
[DebuggerDisplay("ScreenPoint ({X}, {Y})")]
public readonly record struct ScreenPoint(int X, int Y);
