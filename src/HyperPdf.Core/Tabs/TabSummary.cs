// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Core.Tabs;

/// <summary>What the tab finder searches for one open tab.</summary>
/// <param name="FileName">The file name.</param>
/// <param name="Title">The document title.</param>
/// <param name="Folder">The folder path.</param>
[DebuggerDisplay("TabSummary: {FileName}")]
public readonly record struct TabSummary(string FileName, string Title, string Folder);
