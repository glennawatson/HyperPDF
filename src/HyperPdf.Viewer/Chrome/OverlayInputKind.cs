// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Viewer.Chrome;

/// <summary>What the user did to a menu, a dialog or the search bar.</summary>
internal enum OverlayInputKind
{
    /// <summary>Pressed one of its controls.</summary>
    Press = 0,

    /// <summary>Pressed a key.</summary>
    Key = 1,

    /// <summary>Typed a character.</summary>
    Text = 2,

    /// <summary>Scrolled a list.</summary>
    Scroll = 3,

    /// <summary>Sent it away without an answer, by a press outside it or by opening something in its place.</summary>
    Dismiss = 4,

    /// <summary>The pointer went down over a drawing pad of the dialog.</summary>
    PadDown = 5,

    /// <summary>The pointer moved while it was held down on a drawing pad.</summary>
    PadMove = 6,

    /// <summary>The pointer was let go after a stroke on a drawing pad.</summary>
    PadUp = 7,
}
