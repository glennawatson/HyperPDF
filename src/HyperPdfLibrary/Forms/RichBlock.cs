// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Forms;

/// <summary>A rich text paragraph with its runs' fonts and sizes resolved and its lines broken.</summary>
/// <param name="Flow">The paragraph's characters as one row of units.</param>
/// <param name="Fonts">The font of each run.</param>
/// <param name="Sizes">The size of each run.</param>
/// <param name="Runs">The runs.</param>
/// <param name="Lines">The lines, as ranges of units.</param>
[DebuggerDisplay("RichBlock: {Lines.Count} lines")]
internal sealed record RichBlock(RichFlow Flow, FormFont[] Fonts, float[] Sizes, List<RichRun> Runs, List<TextLine> Lines);
