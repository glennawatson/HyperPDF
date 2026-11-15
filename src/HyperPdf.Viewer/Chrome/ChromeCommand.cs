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

    /// <summary>Shows the tab of the side panel named by the item's index: pages, bookmarks, notes, files or layers.</summary>
    SidebarMode = 21,

    /// <summary>Shows or hides the bar of marking tools.</summary>
    Annotate = 22,

    /// <summary>Chooses the marking tool named by the item's index.</summary>
    MarkTool = 23,

    /// <summary>Takes back the last change to the marks.</summary>
    Undo = 24,

    /// <summary>Puts back the change that was taken back.</summary>
    Redo = 25,

    /// <summary>Changes the colour new marks are made in to the next one.</summary>
    MarkColor = 26,

    /// <summary>Changes the line width new marks are made with to the next one.</summary>
    MarkWidth = 27,

    /// <summary>Removes the mark that is picked.</summary>
    DeleteMark = 28,

    /// <summary>Changes the file dialog between listing PDF files and listing every file.</summary>
    FileKinds = 29,

    /// <summary>Shows the tab after the one shown.</summary>
    NextTab = 30,

    /// <summary>Shows the tab before the one shown.</summary>
    PreviousTab = 31,

    /// <summary>Runs the action of a notice or tool bar named by the item's index.</summary>
    ToolAction = 32,

    /// <summary>Turns matching of capital and small letters in the search bar on or off.</summary>
    FindMatchCase = 33,

    /// <summary>Turns finding whole words only in the search bar on or off.</summary>
    FindWholeWord = 34,

    /// <summary>Turns ignoring of accents in the search bar on or off.</summary>
    FindIgnoreAccents = 35,

    /// <summary>Plays the sound or film, or carries on after a pause.</summary>
    MediaPlay = 36,

    /// <summary>Pauses the sound or film.</summary>
    MediaPause = 37,

    /// <summary>Stops the sound or film and goes back to its start.</summary>
    MediaStop = 38,

    /// <summary>Goes back ten seconds.</summary>
    MediaBack = 39,

    /// <summary>Goes forward ten seconds.</summary>
    MediaForward = 40,

    /// <summary>Shows the film in a window of its own.</summary>
    MediaPicture = 41,
}
