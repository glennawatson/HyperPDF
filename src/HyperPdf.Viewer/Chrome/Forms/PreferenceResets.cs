// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Viewer.Chrome.Forms;

/// <summary>
/// What one open preferences window remembers: the settings as they were before every one was put back to its default, so
/// that can be undone while the window is open. A new window starts with nothing remembered.
/// </summary>
[DebuggerDisplay("PreferenceResets: {Before.Count} kept")]
internal sealed class PreferenceResets
{
    /// <summary>Gets the values the changed settings had before they were put back. Empty when there is nothing to bring back.</summary>
    internal List<PreferenceValue> Before { get; } = [];
}
