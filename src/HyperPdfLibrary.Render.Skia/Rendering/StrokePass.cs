// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using SkiaSharp;

namespace HyperPdfLibrary.Rendering;

/// <summary>How one stroke is drawn.</summary>
/// <param name="Alpha">The alpha to stroke with.</param>
/// <param name="Blend">The blend mode to stroke with.</param>
/// <param name="Hairline">Whether a one pixel hairline is drawn, so the line never falls below a device pixel.</param>
/// <param name="Body">Whether the line is drawn at its own width.</param>
[DebuggerDisplay("StrokePass: alpha {Alpha} body {Body} hairline {Hairline}")]
internal readonly record struct StrokePass(float Alpha, SKBlendMode Blend, bool Hairline, bool Body);
