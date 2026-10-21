// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Layout;

namespace HyperPdf.Presentation.Input;

/// <summary>What the toolkit must do after the canvas has dealt with a pointer or key event, which only the toolkit can do.</summary>
[DebuggerDisplay("InputEffects: handled={Handled}")]
public sealed class InputEffects
{
    /// <summary>Gets or sets a value indicating whether the canvas used the event, so the toolkit should not pass it on.</summary>
    public bool Handled { get; set; }

    /// <summary>Gets or sets a value indicating whether the keyboard should come to the canvas.</summary>
    public bool Focus { get; set; }

    /// <summary>Gets or sets a value indicating whether the pointer should be kept by the canvas until it is released.</summary>
    public bool Capture { get; set; }

    /// <summary>Gets or sets a value indicating whether the canvas should let go of the pointer.</summary>
    public bool ReleaseCapture { get; set; }

    /// <summary>Gets or sets the pointer shape to show, where <see cref="ViewerCursor.Inherit"/> lets it follow what is under it, or <see langword="null"/> to leave it as it is.</summary>
    public ViewerCursor? Cursor { get; set; }

    /// <summary>Gets or sets a value indicating whether the toolkit should open the right-click menu for the press.</summary>
    public bool ShowContextMenu { get; set; }

    /// <summary>Gets or sets the choice field whose options the toolkit should open as a menu, or <see langword="null"/> for none.</summary>
    public FormField? Choices { get; set; }

    /// <summary>Gets or sets the area of the canvas to take a picture of, or <see langword="null"/> for none.</summary>
    public LayoutRect? Snapshot { get; set; }

    /// <summary>Gets or sets the text to put on the clipboard, or <see langword="null"/> for none.</summary>
    public string? CopyText { get; set; }

    /// <summary>Forgets everything the last event asked for, so the same instance is reused by the next.</summary>
    public void Reset()
    {
        Handled = false;
        Focus = false;
        Capture = false;
        ReleaseCapture = false;
        Cursor = null;
        ShowContextMenu = false;
        Choices = null;
        Snapshot = null;
        CopyText = null;
    }
}
