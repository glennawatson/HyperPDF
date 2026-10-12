// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Geometry;

/// <summary>A point in unrotated page space, in points, with the origin at the top-left corner and Y growing downwards.</summary>
/// <param name="X">The horizontal position in points.</param>
/// <param name="Y">The vertical position in points.</param>
[DebuggerDisplay("PagePoint: ({X}, {Y})")]
public readonly record struct PagePoint(float X, float Y);
