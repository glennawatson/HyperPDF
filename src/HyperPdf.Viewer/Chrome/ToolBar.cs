// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Chrome;

/// <summary>One bar under the toolbar that tells what a tool or a notice says now and offers what can be done about it.</summary>
/// <param name="Text">What the tool or notice says.</param>
/// <param name="Actions">What can be done, each with the command it runs and the icon that shows it.</param>
[DebuggerDisplay("ToolBar: {Text}")]
internal sealed record ToolBar(string Text, BarAction[] Actions)
{
    /// <summary>
    /// Gets a value indicating whether the words have a line to themselves, above the buttons. A bar whose words change
    /// while it is used, as the state of reading aloud does, sets it, so its buttons stay where they are.
    /// </summary>
    internal bool TextOnOwnLine { get; init; }
}
