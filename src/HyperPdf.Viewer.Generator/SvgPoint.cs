// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Generator;

/// <summary>A point of an icon's outline.</summary>
/// <param name="X">The distance across.</param>
/// <param name="Y">The distance down.</param>
[DebuggerDisplay("SvgPoint: {X}, {Y}")]
internal readonly record struct SvgPoint(double X, double Y);
