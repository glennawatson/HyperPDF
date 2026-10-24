// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Platform.Linux.Accessibility.AtSpi;

/// <summary>A corner that coordinates are measured from, in screen pixels.</summary>
/// <param name="X">Pixels from the left of the screen.</param>
/// <param name="Y">Pixels from the top of the screen.</param>
[DebuggerDisplay("Origin: {X}, {Y}")]
internal readonly record struct Origin(int X, int Y);
