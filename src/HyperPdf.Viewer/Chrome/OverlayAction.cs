// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Viewer.Chrome;

/// <summary>
/// What a press on a control means to the menu or dialog it is in. It is an input to that dialog's own state, such as which row is
/// picked or whether the dialog was agreed to. Anything that acts on the document or the window is a command on the control instead.
/// </summary>
public enum OverlayAction
{
    /// <summary>The press means nothing to the dialog.</summary>
    None = 0,

    /// <summary>Agree to the dialog.</summary>
    Accept = 1,

    /// <summary>Close the dialog without agreeing.</summary>
    Cancel = 2,

    /// <summary>Pick the row of a list.</summary>
    PickRow = 3,

    /// <summary>Pick an entry of a menu, or one value of a choice.</summary>
    PickOption = 4,

    /// <summary>Show the folder above the one a file dialog lists.</summary>
    ParentFolder = 5,

    /// <summary>Switch a file dialog between PDF files and all files.</summary>
    FileKinds = 6,
}
