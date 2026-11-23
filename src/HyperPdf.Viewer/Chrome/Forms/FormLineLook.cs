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
internal readonly record struct FormLineLook(int LabelWidth, int Padding, int Gap, TextRuns Runs, TextStyle Style)
{
    /// <summary>
    /// Gets the room kept at the end of every line for the button that puts a setting back, or 0 for a dialog with no such
    /// button. Every line keeps the room, so a control does not move when the button of its line comes and goes.
    /// </summary>
    internal int ResetRoom { get; init; }

    /// <summary>Gets the name of the button that puts a setting back, which a screen reader says.</summary>
    internal string ResetName { get; init; } = string.Empty;
}
