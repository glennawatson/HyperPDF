// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>The widget rich text is drawn for.</summary>
/// <param name="Context">The widget.</param>
/// <param name="Frame">The frame.</param>
/// <param name="FieldSize">The size the field's default appearance sets; 0 for none.</param>
/// <param name="FieldColor">The field's text colour.</param>
[DebuggerDisplay("RichTarget: {Frame}")]
internal readonly record struct RichTarget(AppearanceContext Context, FormFrame Frame, float FieldSize, DaColor FieldColor);
