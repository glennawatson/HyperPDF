// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Viewer.Text;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>What the lines of a dialog are placed with.</summary>
/// <param name="LabelWidth">The width of the column of setting names.</param>
/// <param name="Padding">The room between the dialog's edge and its controls.</param>
/// <param name="Gap">The small space kept between two controls.</param>
/// <param name="Runs">The text measurer.</param>
/// <param name="Style">How the text looks.</param>
internal readonly record struct FormLineLook(int LabelWidth, int Padding, int Gap, TextRuns Runs, TextStyle Style);
