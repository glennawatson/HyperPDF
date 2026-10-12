// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Settings;

namespace HyperPdf.App.ViewModels;

/// <summary>Decides how a tab responds to a change other programs may have made to its open file.</summary>
internal static class FileChangeResponses
{
    /// <summary>How long after saving a file change notice is taken to be the tab's own save.</summary>
    internal const long SelfSaveWindowMilliseconds = 2000;

    /// <summary>Decides how to respond to a file change notice. Unsaved edits are never discarded silently.</summary>
    /// <param name="facts">What is known about the file and the tab.</param>
    /// <returns>The response; anything but <see cref="FileChangeResponse.Unchanged"/> takes the current stamp.</returns>
    internal static FileChangeResponse Decide(in FileChangeFacts facts)
    {
        if (facts.SinceSave < SelfSaveWindowMilliseconds)
        {
            return FileChangeResponse.OwnSave;
        }

        // macOS can report a write made just before watching began; a file whose time and length are unchanged is the one loaded.
        if (facts.Current == facts.Loaded)
        {
            return FileChangeResponse.Unchanged;
        }

        return facts.Action == FileChangeAction.AskToReload || facts.HasUnsavedChanges ? FileChangeResponse.Ask : FileChangeResponse.Reload;
    }
}
