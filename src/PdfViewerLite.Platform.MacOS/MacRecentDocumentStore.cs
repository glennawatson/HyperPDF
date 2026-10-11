// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using PdfViewerLite.Core.Platform;

namespace PdfViewerLite.Platform.MacOS;

/// <summary>
/// Remembers recent documents for the start page and tells macOS about each one through <c>NSDocumentController</c>, so
/// they appear in the Dock menu's recent items and the Apple menu's Recent Items.
/// </summary>
[DebuggerDisplay("MacRecentDocumentStore: macOS recent documents")]
public sealed class MacRecentDocumentStore : IRecentDocumentStore
{
    /// <summary>The app's own list.</summary>
    private readonly JsonRecentDocumentStore _store;

    /// <summary>Initializes a new instance of the <see cref="MacRecentDocumentStore"/> class.</summary>
    /// <param name="filePath">The app's own list file.</param>
    public MacRecentDocumentStore(string filePath) => _store = new(filePath, TimeProvider.System);

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public IReadOnlyList<RecentDocument> GetRecent(int maxCount) => _store.GetRecent(maxCount);

    /// <inheritdoc/>
    public void Add(string filePath)
    {
        _store.Add(filePath);
        var full = Path.GetFullPath(filePath);
        Foundation.InPool(() =>
        {
            var controller = NativeMethods.Send(NativeMethods.GetClass("NSDocumentController"), NativeMethods.Selector("sharedDocumentController"));
            _ = NativeMethods.Send(controller, NativeMethods.Selector("noteNewRecentDocumentURL:"), Foundation.FileUrl(full));
        });
    }

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Remove(string filePath) => _store.Remove(filePath);

    /// <inheritdoc/>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void Clear() => _store.Clear();
}
