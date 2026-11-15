// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Chrome;

/// <summary>What the file dialog shows: one folder, what was typed, and the answer once there is one.</summary>
/// <param name="Folder">The folder shown.</param>
/// <param name="Entry">The text typed: a path to open, or the name to save as.</param>
/// <param name="Names">The names in the folder shown: folders first, then files.</param>
/// <param name="FolderCount">The folders at the start of <paramref name="Names"/>.</param>
/// <param name="First">The first row of the list shown.</param>
/// <param name="Chosen">The names of the files ticked in the folder shown, when several files can be opened at once.</param>
/// <param name="AllFiles">Whether every file is listed rather than PDF files only.</param>
/// <param name="Saving">Whether the dialog asks where to save rather than what to open.</param>
/// <param name="Many">Whether several files can be opened at once: a press on a file ticks it, and the open button opens every ticked file.</param>
/// <param name="PicksFolder">Whether the dialog asks for a folder rather than a file. The answer is then the folder shown.</param>
/// <param name="Answered">Whether the dialog was answered. Only the first answer counts.</param>
/// <param name="Answer">The paths chosen, or none when the dialog was cancelled.</param>
[DebuggerDisplay("FileState: {Folder}, {Names.Length} names, answered {Answered}")]
internal sealed record FileState(
    string Folder,
    string Entry,
    string[] Names,
    int FolderCount,
    int First,
    string[] Chosen,
    bool AllFiles,
    bool Saving,
    bool Many,
    bool PicksFolder,
    bool Answered,
    string[] Answer);
