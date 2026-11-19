// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Chrome;

/// <summary>What sets one button of a group apart from the others that run the same command.</summary>
/// <param name="Index">The button's place in its group.</param>
/// <param name="Parameter">The value the button passes to the command.</param>
/// <param name="Selected">Whether it is the chosen one of the group.</param>
[DebuggerDisplay("GroupChoice: {Index}, selected {Selected}")]
internal readonly record struct GroupChoice(int Index, object? Parameter, bool Selected)
{
    /// <summary>Gets the help specific to this action.</summary>
    internal string? Help { get; init; }
}
