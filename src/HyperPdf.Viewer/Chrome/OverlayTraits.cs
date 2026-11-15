// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Viewer.Chrome;

/// <summary>How a menu, a dialog or the search bar behaves towards the rest of the window while it is open.</summary>
[Flags]
internal enum OverlayTraits
{
    /// <summary>It lies over the window and leaves the rest of it as it is.</summary>
    None = 0,

    /// <summary>The rest of the window is dimmed and cannot be pressed.</summary>
    Modal = 1 << 0,

    /// <summary>Every key goes to it, not only typing, Enter, Escape and Backspace.</summary>
    TakesAllKeys = 1 << 1,

    /// <summary>A press outside it sends it away.</summary>
    ClosesOnOutsidePress = 1 << 2,

    /// <summary>It uses Tab itself, so Tab does not move the keyboard between its controls.</summary>
    UsesTab = 1 << 3,

    /// <summary>It stands in for the pages, as focus mode and the start view do: what opens next goes over it, and it comes back when that closes.</summary>
    StaysUnder = 1 << 4,

    /// <summary>It has a drawing pad, so a press that is not on a control, and the pointer moving after it, go to it.</summary>
    TakesPointer = 1 << 5,
}
