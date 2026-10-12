// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Settings;

/// <summary>What happens when an open file changes on disk.</summary>
public enum FileChangeAction
{
    /// <summary>Reload in place, keeping the current page.</summary>
    ReloadAutomatically = 0,

    /// <summary>Show a steady bar offering to reload; nothing changes until the user chooses.</summary>
    AskToReload = 1,
}
