// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Icons;

/// <summary>A point, or a pair of radii, on the grid an icon is drawn on.</summary>
/// <param name="X">The distance across in grid units.</param>
/// <param name="Y">The distance down in grid units.</param>
[DebuggerDisplay("IconPoint: {X}, {Y}")]
internal readonly record struct IconPoint(float X, float Y);
