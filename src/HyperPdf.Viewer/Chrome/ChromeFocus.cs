// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// Where the keyboard is among the window's controls. It remembers what the control does as well as where it is in the list, so when
/// the list is laid out again the keyboard finds the same control, and moves to a near one when that control is gone.
/// </summary>
/// <param name="Index">The place of the control in its list, or -1 while the keyboard is on the page.</param>
/// <param name="InOverlay">Whether the control is in the open menu, dialog or search bar rather than the bars.</param>
/// <param name="Command">What the control does.</param>
/// <param name="Target">The control's own number among those that do the same.</param>
/// <param name="Kind">What kind of control it is.</param>
[DebuggerDisplay("ChromeFocus: {Index} in overlay {InOverlay}")]
internal sealed record ChromeFocus(int Index, bool InOverlay, ChromeCommand Command, int Target, ChromeItemKind Kind)
{
    /// <summary>Gets the keyboard on the page, on no control.</summary>
    internal static ChromeFocus None { get; } = new(-1, false, ChromeCommand.None, 0, default);

    /// <summary>Puts the keyboard on a control.</summary>
    /// <param name="items">The controls.</param>
    /// <param name="inOverlay">Whether the controls are those of the open menu, dialog or search bar.</param>
    /// <param name="index">The place of the control in the list.</param>
    /// <returns>Where the keyboard is.</returns>
    internal static ChromeFocus At(List<ChromeItem> items, bool inOverlay, int index)
    {
        var item = items[index];
        return new(index, inOverlay, item.Command, item.Index, item.Kind);
    }

    /// <summary>Tells whether the keyboard can stop on a control.</summary>
    /// <param name="item">The control.</param>
    /// <returns><see langword="true"/> when it does something and can be pressed.</returns>
    internal static bool CanStopOn(ChromeItem item) => item.Enabled && item.Command != ChromeCommand.None;
}
