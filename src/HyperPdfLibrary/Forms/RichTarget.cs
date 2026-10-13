// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>What rich text is drawn into: the widget, its frame, its default appearance and the bars to draw after the text.</summary>
/// <param name="Context">The widget.</param>
/// <param name="Frame">The frame.</param>
/// <param name="Rules">Receives the underlines and strike-throughs.</param>
/// <param name="FieldSize">The font size the field's default appearance sets; 0 for none.</param>
/// <param name="FieldColor">The colour operators of the field's default appearance, ending in a line feed, for text that sets no colour.</param>
[DebuggerDisplay("RichTarget: {Frame}")]
internal sealed record RichTarget(AppearanceContext Context, FormFrame Frame, List<RichRule> Rules, float FieldSize, byte[] FieldColor);
