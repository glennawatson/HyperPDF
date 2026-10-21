// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.ViewModels.Viewing;

/// <summary>The keys the document view reacts to, named the same whichever toolkit reports them.</summary>
public enum ViewerKey
{
    /// <summary>Any key the document view does not use.</summary>
    Other = 0,

    /// <summary>The left arrow key.</summary>
    Left = 1,

    /// <summary>The right arrow key.</summary>
    Right = 2,

    /// <summary>The up arrow key.</summary>
    Up = 3,

    /// <summary>The down arrow key.</summary>
    Down = 4,

    /// <summary>The Page Up key.</summary>
    PageUp = 5,

    /// <summary>The Page Down key.</summary>
    PageDown = 6,

    /// <summary>The Home key, which goes to the first page.</summary>
    Home = 7,

    /// <summary>The End key, which goes to the last page.</summary>
    End = 8,

    /// <summary>The Enter or Return key.</summary>
    Enter = 9,

    /// <summary>The Escape key, which cancels what is under way.</summary>
    Escape = 10,

    /// <summary>The Delete key, which removes the picked item.</summary>
    Delete = 11,

    /// <summary>The Backspace key.</summary>
    Back = 12,

    /// <summary>The Tab key, which moves to the next field.</summary>
    Tab = 13,

    /// <summary>The space bar.</summary>
    Space = 14,

    /// <summary>The F2 key, which edits the picked item.</summary>
    F2 = 15,

    /// <summary>The plus key, on the main keys or the number pad.</summary>
    Plus = 16,

    /// <summary>The minus key, on the main keys or the number pad.</summary>
    Minus = 17,
}
