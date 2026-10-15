// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Filters;
using Microsoft.Win32.SafeHandles;

namespace HyperPdfLibrary.IO;

/// <summary>Opens a <see cref="PdfByteSource"/> for a file path.</summary>
public static class PdfByteSources
{
    /// <summary>Opens a file.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="kind">How to read the file.</param>
    /// <param name="cacheBytes">The page cache budget of a <see cref="StreamPdfByteSource"/>.</param>
    /// <returns>The source; the caller disposes it.</returns>
    /// <exception cref="IOException">The file cannot be opened, read or mapped.</exception>
    /// <exception cref="UnauthorizedAccessException">The file cannot be read.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfByteSource Open(string path, PdfSourceKind kind, long cacheBytes) => Open(path, kind, cacheBytes, null);

    /// <summary>
    /// Opens a file, reading with async I/O where the kind reads the file: <see cref="PdfSourceKind.Memory"/> reads it whole
    /// and <see cref="PdfSourceKind.Stream"/> opens it for overlapped reads. Mapped and automatic sources open as <see cref="Open(string, PdfSourceKind, long)"/> does.
    /// </summary>
    /// <param name="path">The file path.</param>
    /// <param name="kind">How to read the file.</param>
    /// <param name="cacheBytes">The page cache budget of a <see cref="StreamPdfByteSource"/>.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The source; the caller disposes it.</returns>
    /// <exception cref="IOException">The file cannot be opened, read or mapped.</exception>
    /// <exception cref="UnauthorizedAccessException">The file cannot be read.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<PdfByteSource> OpenAsync(string path, PdfSourceKind kind, long cacheBytes, CancellationToken cancellationToken) =>
        OpenAsync(path, kind, cacheBytes, null, cancellationToken);

    /// <summary>Opens a file with the registered host's pools.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="kind">How to read the file.</param>
    /// <param name="cacheBytes">The cache budget.</param>
    /// <param name="pools">The registered host's pools, when available.</param>
    /// <returns>The source, owned by the caller.</returns>
    internal static PdfByteSource Open(string path, PdfSourceKind kind, long cacheBytes, ScratchPools? pools)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        return kind switch
        {
            PdfSourceKind.Memory => MemoryPdfByteSource.ReadFile(path),
            PdfSourceKind.Mapped => MappedPdfByteSource.Open(path),
            PdfSourceKind.Stream => StreamPdfByteSource.Open(path, cacheBytes, pools),
            _ => OpenAutomatic(path, cacheBytes, pools),
        };
    }

    /// <summary>Opens a file asynchronously with the registered host's pools.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="kind">How to read the file.</param>
    /// <param name="cacheBytes">The cache budget.</param>
    /// <param name="pools">The registered host's pools, when available.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns>The source, owned by the caller.</returns>
    internal static async ValueTask<PdfByteSource> OpenAsync(string path, PdfSourceKind kind, long cacheBytes, ScratchPools? pools, CancellationToken cancellationToken)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        cancellationToken.ThrowIfCancellationRequested();
        switch (kind)
        {
            case PdfSourceKind.Memory:
                {
                    return await MemoryPdfByteSource.ReadFileAsync(path, cancellationToken).ConfigureAwait(false);
                }

            case PdfSourceKind.Stream:
                {
                    return StreamPdfByteSource.OpenAsynchronous(path, cacheBytes, pools);
                }

            default:
                {
                    return Open(path, kind, cacheBytes, pools);
                }
        }
    }

    /// <summary>Maps a file, or reads it through its handle when it is empty or cannot be mapped.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="cacheBytes">The page cache budget for the fallback.</param>
    /// <param name="pools">The registered host's pools, when available.</param>
    /// <returns>The source.</returns>
    private static PdfByteSource OpenAutomatic(string path, long cacheBytes, ScratchPools? pools)
    {
        var handle = File.OpenHandle(path, FileMode.Open, FileAccess.Read, FileShare.ReadWrite | FileShare.Delete);
        try
        {
            var mapped = TryMap(handle);
            if (mapped is not null)
            {
                handle.Dispose();
                return mapped;
            }

            return new StreamPdfByteSource(handle, cacheBytes, true, pools);
        }
        catch
        {
            handle.Dispose();
            throw;
        }
    }

    /// <summary>Maps a file when it can be mapped.</summary>
    /// <param name="handle">The file handle.</param>
    /// <returns>The source, or <see langword="null"/> on Windows, for an empty file, or on a file system that cannot map it.</returns>
    private static MappedPdfByteSource? TryMap(SafeFileHandle handle)
    {
        // Windows will not delete a mapped file, so saving over an open document would leave the old file behind.
        if (OperatingSystem.IsWindows() || RandomAccess.GetLength(handle) <= 0)
        {
            return null;
        }

        try
        {
            return MappedPdfByteSource.Open(handle);
        }
        catch (IOException)
        {
            return null;
        }
        catch (UnauthorizedAccessException)
        {
            return null;
        }
        catch (NotSupportedException)
        {
            return null;
        }
    }
}
