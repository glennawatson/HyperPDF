// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Presentation.Input;

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

    /// <summary>The A key.</summary>
    A = 18,

    /// <summary>The C key.</summary>
    C = 19,

    /// <summary>The F key.</summary>
    F = 20,

    /// <summary>The O key.</summary>
    O = 21,

    /// <summary>The S key.</summary>
    S = 22,

    /// <summary>The W key.</summary>
    W = 23,

    /// <summary>The V key.</summary>
    V = 24,

    /// <summary>The B key.</summary>
    B = 25,

    /// <summary>The E key.</summary>
    E = 26,

    /// <summary>The G key.</summary>
    G = 27,

    /// <summary>The I key.</summary>
    I = 28,

    /// <summary>The N key.</summary>
    N = 29,

    /// <summary>The P key.</summary>
    P = 30,

    /// <summary>The T key.</summary>
    T = 31,

    /// <summary>The Y key.</summary>
    Y = 32,

    /// <summary>The Z key.</summary>
    Z = 33,

    /// <summary>The comma key.</summary>
    Comma = 34,

    /// <summary>The full stop key.</summary>
    Period = 35,

    /// <summary>The 0 key.</summary>
    Digit0 = 36,

    /// <summary>The 1 key.</summary>
    Digit1 = 37,

    /// <summary>The 2 key.</summary>
    Digit2 = 38,

    /// <summary>The 4 key.</summary>
    Digit4 = 39,

    /// <summary>The F3 key, which finds the next match.</summary>
    F3 = 40,

    /// <summary>The F4 key.</summary>
    F4 = 41,

    /// <summary>The F6 key, which moves the keyboard between the page and the window's controls.</summary>
    F6 = 42,

    /// <summary>The F7 key, which turns the keyboard cursor on the page on or off.</summary>
    F7 = 43,

    /// <summary>The F9 key, which shows or hides the side panel.</summary>
    F9 = 44,

    /// <summary>The F10 key, which opens the main menu.</summary>
    F10 = 45,

    /// <summary>The menu key, which opens the menu of what the keyboard is on.</summary>
    Menu = 46,

    /// <summary>The H key, which with Control turns reading mode on or off.</summary>
    H = 47,

    /// <summary>The M key, which with Control and Shift turns measuring on or off.</summary>
    M = 48,

    /// <summary>The F5 key, which reloads the file, and with Shift starts a presentation.</summary>
    F5 = 49,

    /// <summary>The F11 key, which puts the window into or out of full screen.</summary>
    F11 = 50,

    /// <summary>The D key, which with Alt opens the Document menu.</summary>
    D = 51,
}
