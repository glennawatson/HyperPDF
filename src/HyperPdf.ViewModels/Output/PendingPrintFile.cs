// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.ViewModels.Output;

/// <summary>
/// The file a print preview build is writing. It is deleted when the build is replaced or the window closes before the file was
/// claimed, even when that happens while the build is still running.
/// </summary>
internal sealed class PendingPrintFile : IDisposable
{
    /// <summary>The built file, or <see langword="null"/> before the build ends, after it failed or once it was claimed.</summary>
    private string? _path;

    /// <summary>Whether the build was replaced or the window closed.</summary>
    private int _disposed;

    /// <summary>Gets a value indicating whether the build was replaced or the window closed.</summary>
    internal bool IsDisposed => Volatile.Read(ref _disposed) == 1;

    /// <summary>Deletes the file when it was not claimed.</summary>
    public void Dispose()
    {
        _ = Interlocked.Exchange(ref _disposed, 1);
        PrintPreviewViewModel.Delete(Interlocked.Exchange(ref _path, null));
    }

    /// <summary>Keeps the file the build wrote, or deletes it at once when the build was already replaced.</summary>
    /// <param name="path">The built file, or <see langword="null"/> when the build failed.</param>
    internal void Set(string? path)
    {
        Volatile.Write(ref _path, path);
        if (IsDisposed)
        {
            Dispose();
        }
    }

    /// <summary>Takes the file so it is no longer deleted.</summary>
    /// <returns>The built file, or <see langword="null"/> when the build failed.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal string? Claim() => Interlocked.Exchange(ref _path, null);
}
