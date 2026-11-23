// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>The value one row of the preferences window had, kept so it can be brought back.</summary>
/// <param name="Row">The row's place in the window's list of rows.</param>
/// <param name="Index">The chosen value of a list, or 1 for a switch that was on.</param>
/// <param name="Text">The text of a typed setting.</param>
[DebuggerDisplay("PreferenceValue: row {Row} = {Index} '{Text}'")]
internal readonly record struct PreferenceValue(int Row, int Index, string Text);
