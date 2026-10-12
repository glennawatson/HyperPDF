// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Text.Fonts;

/// <summary>The installed face chosen for a family and style, and what has to be drawn in because the family lacks it.</summary>
/// <param name="Face">The face.</param>
/// <param name="SynthesizeBold">Whether bold is drawn by thickening the outlines, as the family has no bold face.</param>
/// <param name="SynthesizeItalic">Whether italic is drawn by slanting, as the family has no italic face.</param>
[DebuggerDisplay("FontMatch: {Face.Family} {Face.Style}")]
public sealed record FontMatch(FontFace Face, bool SynthesizeBold, bool SynthesizeItalic);
