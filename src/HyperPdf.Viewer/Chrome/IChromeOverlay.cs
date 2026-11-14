// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Presentation.Input;
using HyperPdf.Viewer.Text;
using HyperPdf.Windowing.Rendering;

namespace HyperPdf.Viewer.Chrome;

/// <summary>Something shown over the window for a while: a menu, a dialog or the search bar. It takes the keyboard while it is open.</summary>
internal interface IChromeOverlay
{
    /// <summary>Gets a value indicating whether the rest of the window is dimmed and cannot be pressed while this is open.</summary>
    bool IsModal { get; }

    /// <summary>Gets a value indicating whether every key goes to the overlay. When it does not, only typing, Enter, Escape and Backspace do.</summary>
    bool TakesAllKeys { get; }

    /// <summary>Gets a value indicating whether a press outside the overlay closes it.</summary>
    bool ClosesOnOutsidePress { get; }

    /// <summary>Gets a number that changes whenever what the overlay shows changes, so it is laid out again.</summary>
    int Version { get; }

    /// <summary>Lays the overlay out.</summary>
    /// <param name="items">Receives the controls, back to front.</param>
    /// <param name="runs">The text measurer.</param>
    /// <param name="metrics">The sizes.</param>
    /// <param name="size">The window size in device pixels.</param>
    void Build(List<ChromeItem> items, TextRuns runs, in ChromeMetrics metrics, SurfaceSize size);

    /// <summary>Handles a press on one of the overlay's controls.</summary>
    /// <param name="item">The control.</param>
    /// <returns><see langword="true"/> to stay open, <see langword="false"/> to close.</returns>
    bool Press(ChromeItem item);

    /// <summary>Handles a key.</summary>
    /// <param name="key">The key.</param>
    /// <param name="modifiers">The modifier keys held.</param>
    /// <returns><see langword="true"/> to stay open, <see langword="false"/> to close.</returns>
    bool PressKey(ViewerKey key, ViewerModifiers modifiers);

    /// <summary>Handles a typed character.</summary>
    /// <param name="scalar">The Unicode scalar value.</param>
    void Type(int scalar);

    /// <summary>Scrolls a list in the overlay.</summary>
    /// <param name="rows">The rows to move down, negative to move up.</param>
    void Scroll(int rows);

    /// <summary>Tells the overlay it was closed without an answer, by a press outside it or because the window closed.</summary>
    void Dismiss();
}
