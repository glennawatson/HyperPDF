// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Platform;

namespace PdfViewerLite.Platform.Windows.Shell;

/// <summary>
/// Remembers recent documents for the start page and tells Windows about each one, so it appears in the taskbar Jump
/// List and Explorer's recent items. Windows' own list holds shortcuts to every app's files, so the app keeps its own.
/// </summary>
[DebuggerDisplay("WindowsRecentDocumentStore: Windows recent documents")]
public sealed class WindowsRecentDocumentStore : IRecentDocumentStore
{
    /// <summary>Tells the shell the item is a UTF-16 path (<c>SHARD_PATHW</c>).</summary>
    private const uint PathItem = 3;

    /// <summary>The app's own list.</summary>
    private readonly JsonRecentDocumentStore _store;

    /// <summary>Initializes a new instance of the <see cref="WindowsRecentDocumentStore"/> class.</summary>
    /// <param name="filePath">The app's own list file.</param>
    public WindowsRecentDocumentStore(string filePath) => _store = new(filePath, TimeProvider.System);

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public IReadOnlyList<RecentDocument> GetRecent(int maxCount) => _store.GetRecent(maxCount);

    /// <inheritdoc/>
    public void Add(string filePath)
    {
        _store.Add(filePath);
        NativeMethods.SHAddToRecentDocs(PathItem, Path.GetFullPath(filePath));
    }

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Remove(string filePath) => _store.Remove(filePath);

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Clear() => _store.Clear();
}
