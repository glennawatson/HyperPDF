// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Viewer.Icons;

/// <summary>The line icons the window's controls show beside or in place of their text. The order is the order of the outlines in <see cref="IconPaths"/>.</summary>
public enum ChromeIcon
{
    /// <summary>No icon.</summary>
    None = 0,

    /// <summary>A folder: open a file.</summary>
    Open = 1,

    /// <summary>A printer.</summary>
    Print = 2,

    /// <summary>A window with a side panel.</summary>
    Sidebar = 3,

    /// <summary>A chevron to the left.</summary>
    Back = 4,

    /// <summary>A chevron to the right.</summary>
    Forward = 5,

    /// <summary>A chevron upwards: the page before.</summary>
    PageUp = 6,

    /// <summary>A chevron downwards: the next page.</summary>
    PageDown = 7,

    /// <summary>A magnifier with a plus.</summary>
    ZoomIn = 8,

    /// <summary>A magnifier with a minus.</summary>
    ZoomOut = 9,

    /// <summary>Arrows to both sides.</summary>
    FitWidth = 10,

    /// <summary>A whole page.</summary>
    FitPage = 11,

    /// <summary>A turn anticlockwise.</summary>
    RotateLeft = 12,

    /// <summary>A turn clockwise.</summary>
    RotateRight = 13,

    /// <summary>Two pages side by side.</summary>
    Dual = 14,

    /// <summary>A magnifier.</summary>
    Search = 15,

    /// <summary>A moon.</summary>
    Night = 16,

    /// <summary>Three lines: the menu.</summary>
    Menu = 17,

    /// <summary>A cross.</summary>
    Close = 18,

    /// <summary>A plus.</summary>
    Add = 19,

    /// <summary>A page with a folded corner.</summary>
    Document = 20,

    /// <summary>Four small pages.</summary>
    Thumbnails = 21,

    /// <summary>An indented list.</summary>
    Outline = 22,

    /// <summary>A capital and a small letter.</summary>
    Case = 23,

    /// <summary>A word between two bars.</summary>
    WholeWord = 24,

    /// <summary>A letter with an accent.</summary>
    Accent = 25,

    /// <summary>Two waves.</summary>
    Similar = 26,

    /// <summary>A pointer.</summary>
    Cursor = 27,

    /// <summary>A highlighter.</summary>
    Highlight = 28,

    /// <summary>An underlined letter.</summary>
    Underline = 29,

    /// <summary>A struck out letter.</summary>
    Strike = 30,

    /// <summary>A pencil.</summary>
    Draw = 31,

    /// <summary>An insertion mark.</summary>
    Caret = 32,

    /// <summary>A highlighter drawn freehand.</summary>
    FreehandHighlight = 33,

    /// <summary>A sticky note.</summary>
    Note = 34,

    /// <summary>A square, a circle and an arrow.</summary>
    Shape = 35,

    /// <summary>A rubber stamp.</summary>
    Stamp = 36,

    /// <summary>A capital T.</summary>
    Text = 37,

    /// <summary>A signature on a line.</summary>
    Signature = 38,

    /// <summary>An arrow turning back.</summary>
    Undo = 39,

    /// <summary>An arrow turning forward.</summary>
    Redo = 40,

    /// <summary>Lines of growing weight.</summary>
    LineWidth = 41,

    /// <summary>A large and a small letter.</summary>
    TextSize = 42,

    /// <summary>A funnel.</summary>
    Filter = 43,

    /// <summary>A disk.</summary>
    Save = 44,

    /// <summary>A form with a signature.</summary>
    FillSign = 45,

    /// <summary>A note with lines of text.</summary>
    Annotations = 46,

    /// <summary>A paper clip.</summary>
    Attachment = 47,

    /// <summary>A certificate with a seal.</summary>
    Certificate = 48,

    /// <summary>Stacked sheets.</summary>
    Layers = 49,

    /// <summary>A screen with a play mark.</summary>
    Media = 50,

    /// <summary>Three squares and a plus.</summary>
    Parts = 51,

    /// <summary>A price tag.</summary>
    Tags = 52,

    /// <summary>A tick.</summary>
    Done = 53,

    /// <summary>A loudspeaker.</summary>
    Speaker = 54,

    /// <summary>A play mark.</summary>
    Play = 55,

    /// <summary>A pause mark.</summary>
    Pause = 56,

    /// <summary>A skip to the sentence before.</summary>
    PreviousSentence = 57,

    /// <summary>A skip to the next sentence.</summary>
    NextSentence = 58,

    /// <summary>A ruler.</summary>
    Ruler = 59,

    /// <summary>A path with marked corners.</summary>
    Perimeter = 60,

    /// <summary>A hatched shape.</summary>
    Area = 61,

    /// <summary>Lines of text: focus mode.</summary>
    Focus = 62,

    /// <summary>A rectangle.</summary>
    Box = 63,

    /// <summary>An oval.</summary>
    Oval = 64,

    /// <summary>An arrow.</summary>
    Arrow = 65,

    /// <summary>A straight line.</summary>
    Line = 66,

    /// <summary>A square: stop.</summary>
    Stop = 67,

    /// <summary>Three dots: more.</summary>
    More = 68,

    /// <summary>A line at the foot: put the window away.</summary>
    Minimize = 69,

    /// <summary>A square: make the window as big as the screen.</summary>
    Maximize = 70,

    /// <summary>Two squares: put the window back to the size it had.</summary>
    Restore = 71,
}
