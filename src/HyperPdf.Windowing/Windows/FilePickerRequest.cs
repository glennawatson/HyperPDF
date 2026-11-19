// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Windowing.Windows;

/// <summary>Describes a file or folder selection in the desktop's chooser.</summary>
/// <param name="Folder">The initial directory.</param>
/// <param name="SaveName">The suggested filename for a save, or null when opening.</param>
/// <param name="Multiple">Whether several files can be opened.</param>
/// <param name="Directories">Whether to select a directory.</param>
/// <param name="Title">The localised dialog title.</param>
[DebuggerDisplay("{Title} in {Folder}")]
public sealed record FilePickerRequest(string Folder, string? SaveName, bool Multiple, bool Directories, string Title);
