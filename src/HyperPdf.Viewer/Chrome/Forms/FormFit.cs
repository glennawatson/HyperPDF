// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Viewer.Text;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>What the text of a dialog's lines is fitted with: the measurer, the look and the widths of the dialog's two columns.</summary>
/// <param name="Runs">The text measurer.</param>
/// <param name="Style">How the text looks.</param>
/// <param name="Width">The width the text of a line across the dialog must fit.</param>
/// <param name="LabelWidth">The width the name of a setting must fit, in the column of names.</param>
/// <param name="ControlWidth">The width the text of a control must fit, in the column of controls.</param>
/// <param name="IconRoom">The room an icon takes before the text of a control.</param>
internal readonly record struct FormFit(TextRuns Runs, TextStyle Style, int Width, int LabelWidth, int ControlWidth, int IconRoom);
