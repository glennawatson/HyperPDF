// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.App.ViewModels;

/// <summary>How a tab responds when its open file may have changed on disk.</summary>
internal enum FileChangeResponse
{
    /// <summary>The file is the one loaded; nothing changed.</summary>
    Unchanged = 0,

    /// <summary>The change is the tab's own save; remember the new stamp only.</summary>
    OwnSave = 1,

    /// <summary>Offer to reload, because the reader asked to be asked or a reload would lose edits.</summary>
    Ask = 2,

    /// <summary>Reload at once.</summary>
    Reload = 3,
}
