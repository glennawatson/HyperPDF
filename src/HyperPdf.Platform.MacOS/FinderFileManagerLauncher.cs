// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Platform;

namespace HyperPdf.Platform.MacOS;

/// <summary>Reveals a file in the Finder, selected, through <c>NSWorkspace</c>.</summary>
[DebuggerDisplay("FinderFileManagerLauncher: Finder")]
public sealed class FinderFileManagerLauncher : IFileManagerLauncher
{
    /// <inheritdoc/>
    public Task<bool> ShowItemAsync(string filePath, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        var full = Path.GetFullPath(filePath);
        if (!File.Exists(full))
        {
            return Task.FromResult(false);
        }

        Foundation.InPool(() =>
        {
            var url = Foundation.FileUrl(full);
            var urls = NativeMethods.Send(NativeMethods.GetClass("NSArray"), NativeMethods.Selector("arrayWithObject:"), url);
            var workspace = NativeMethods.Send(NativeMethods.GetClass("NSWorkspace"), NativeMethods.Selector("sharedWorkspace"));
            _ = NativeMethods.Send(workspace, NativeMethods.Selector("activateFileViewerSelectingURLs:"), urls);
        });
        return Task.FromResult(true);
    }
}
