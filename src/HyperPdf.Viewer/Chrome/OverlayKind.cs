// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Viewer.Chrome;

/// <summary>What is shown over the window. The window closes some kinds itself when the view model behind them moves on.</summary>
internal enum OverlayKind
{
    /// <summary>A menu.</summary>
    Menu = 0,

    /// <summary>A dialog that waits for an answer.</summary>
    Dialog = 1,

    /// <summary>The search bar.</summary>
    Find = 2,

    /// <summary>A typing box over a form field or a text box on the page.</summary>
    Typing = 3,

    /// <summary>Focus mode, which stands in for the pages.</summary>
    Focus = 4,

    /// <summary>The start view, which stands in for the pages while no document is open.</summary>
    Start = 5,

    /// <summary>A comment bubble beside a note or marked passage on the page.</summary>
    NoteBubble = 6,
}
