// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Platform;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Platform.MacOS;

/// <summary>Reveals a file in the Finder, selected, through <c>NSWorkspace</c>.</summary>
[DebuggerDisplay("FinderFileManagerLauncher: Finder")]
public sealed class FinderFileManagerLauncher : IFileManagerLauncher
{
    /// <inheritdoc/>
    public IObservable<bool> ShowItem(string filePath)
    {
        ArgumentException.ThrowIfNullOrEmpty(filePath);
        return Signal.Defer(() => Signal.Return(Reveal(Path.GetFullPath(filePath))));
    }

    /// <summary>Reveals a file in the Finder.</summary>
    /// <param name="full">The full path.</param>
    /// <returns><see langword="true"/> when the file exists and the Finder was asked to show it.</returns>
    private static bool Reveal(string full)
    {
        if (!File.Exists(full))
        {
            return false;
        }

        Foundation.InPool(() =>
        {
            var url = Foundation.FileUrl(full);
            var urls = NativeMethods.Send(NativeMethods.GetClass("NSArray"), NativeMethods.Selector("arrayWithObject:"), url);
            var workspace = NativeMethods.Send(NativeMethods.GetClass("NSWorkspace"), NativeMethods.Selector("sharedWorkspace"));
            _ = NativeMethods.Send(workspace, NativeMethods.Selector("activateFileViewerSelectingURLs:"), urls);
        });
        return true;
    }
}
