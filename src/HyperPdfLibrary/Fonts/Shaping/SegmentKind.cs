// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>What kind of run is being shaped: its script, direction and writing mode.</summary>
/// <param name="Script">The script.</param>
/// <param name="RightToLeft">Whether the run reads right to left.</param>
/// <param name="Vertical">Whether the run is written vertically.</param>
[DebuggerDisplay("SegmentKind: {Script.Name} rtl {RightToLeft}")]
internal readonly record struct SegmentKind(ShapingScript Script, bool RightToLeft, bool Vertical);
