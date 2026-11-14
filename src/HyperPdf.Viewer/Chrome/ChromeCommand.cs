// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Viewer.Chrome;

/// <summary>What a control of the window does when it is pressed.</summary>
public enum ChromeCommand
{
    /// <summary>Nothing: a label or a panel.</summary>
    None = 0,

    /// <summary>Shows the tab at the item's index.</summary>
    SelectTab = 1,

    /// <summary>Closes the tab at the item's index.</summary>
    CloseTab = 2,

    /// <summary>Asks for files to open.</summary>
    Open = 3,

    /// <summary>Saves the document shown.</summary>
    Save = 4,

    /// <summary>Goes to the page before.</summary>
    PreviousPage = 5,

    /// <summary>Goes to the next page.</summary>
    NextPage = 6,

    /// <summary>Makes the pages smaller.</summary>
    ZoomOut = 7,

    /// <summary>Makes the pages larger.</summary>
    ZoomIn = 8,

    /// <summary>Fits the page width to the window.</summary>
    FitWidth = 9,

    /// <summary>Fits a whole page in the window.</summary>
    FitPage = 10,

    /// <summary>Opens the search bar.</summary>
    Find = 11,

    /// <summary>Opens the main menu.</summary>
    Menu = 12,

    /// <summary>Shows or hides the side panel.</summary>
    Sidebar = 13,

    /// <summary>Runs the menu entry at the item's index.</summary>
    MenuEntry = 14,

    /// <summary>Agrees to what a dialog asks.</summary>
    Accept = 15,

    /// <summary>Closes a dialog or a menu without doing anything.</summary>
    Cancel = 16,

    /// <summary>Picks the row at the item's index in a list.</summary>
    PickRow = 17,

    /// <summary>Goes to the folder above in the file list.</summary>
    ParentFolder = 18,

    /// <summary>Finds the next match.</summary>
    FindNext = 19,

    /// <summary>Finds the match before.</summary>
    FindPrevious = 20,
}
