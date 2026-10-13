// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Fonts.Shaping;

/// <summary>What makes shaping plans differ, so a plan can be found again.</summary>
/// <param name="Script">The script's ISO 15924 code.</param>
/// <param name="Language">The OpenType language tag, or zero.</param>
/// <param name="RightToLeft">Whether the run reads right to left.</param>
/// <param name="Vertical">Whether the run is written vertically.</param>
/// <param name="Features">The feature settings from the caller, as text; empty for none.</param>
[DebuggerDisplay("PlanKey: {Script} {RightToLeft} {Vertical}")]
internal readonly record struct PlanKey(string Script, uint Language, bool RightToLeft, bool Vertical, string Features);
