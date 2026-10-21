// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Settings;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>What is known when another program may have changed a tab's open file.</summary>
/// <param name="Loaded">The file's stamp when it was loaded or last seen to change.</param>
/// <param name="Current">The file's stamp now.</param>
/// <param name="SinceSave">The milliseconds since the tab last saved the file.</param>
/// <param name="Action">What the reader asked to happen when an open file changes.</param>
/// <param name="HasUnsavedChanges">Whether the tab has edits that a reload would lose.</param>
internal readonly record struct FileChangeFacts(DocumentFileStamp Loaded, DocumentFileStamp Current, long SinceSave, FileChangeAction Action, bool HasUnsavedChanges);
