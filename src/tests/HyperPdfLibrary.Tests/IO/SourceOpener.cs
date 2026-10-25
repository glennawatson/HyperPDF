// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.IO;

namespace HyperPdfLibrary.Tests.IO;

/// <summary>Opens the same bytes through each kind of byte source, so tests can compare them.</summary>
internal static class SourceOpener
{
    /// <summary>The cache budget of the stream sources: too small for one page, so the cache holds a single page and every page edge is crossed.</summary>
    internal const long TinyCache = 0;

    /// <summary>Gets the ways a document is opened from bytes.</summary>
    internal static string[] Kinds { get; } = ["memory", "window", "stream", "mapped", "handle"];

    /// <summary>Opens bytes through one kind of source.</summary>
    /// <param name="kind">One of <see cref="Kinds"/>.</param>
    /// <param name="bytes">The file.</param>
    /// <param name="options">The options.</param>
    /// <param name="directory">A temporary directory for the kinds that read a file.</param>
    /// <returns>The document.</returns>
    internal static PdfDocument Open(string kind, byte[] bytes, PdfOpenOptions options, string directory) => kind switch
    {
        "memory" => PdfDocumentReader.OpenWith(bytes, options),
        "window" => PdfDocumentReader.OpenWith(new MemoryPdfByteSource(Offset(bytes)), true, options),
        "stream" => PdfDocumentReader.OpenWith(new StreamPdfByteSource(new MemoryStream(bytes, false), TinyCache, true), true, options),
        "mapped" => PdfDocumentReader.OpenWith(WriteFile(bytes, directory), options with { Source = PdfSourceKind.Mapped }),
        _ => PdfDocumentReader.OpenWith(WriteFile(bytes, directory), options with { Source = PdfSourceKind.Stream, CacheBytes = TinyCache }),
    };

    /// <summary>Creates an empty temporary directory.</summary>
    /// <returns>The directory path.</returns>
    internal static string CreateDirectory()
    {
        var directory = Path.Combine(Path.GetTempPath(), $"hyperpdf-source-{Guid.NewGuid():N}");
        _ = Directory.CreateDirectory(directory);
        return directory;
    }

    /// <summary>Deletes a temporary directory, ignoring files still held open.</summary>
    /// <param name="directory">The directory path.</param>
    internal static void DeleteDirectory(string directory)
    {
        try
        {
            Directory.Delete(directory, true);
        }
        catch (IOException)
        {
        }
        catch (UnauthorizedAccessException)
        {
        }
    }

    /// <summary>Writes bytes to a new file in a directory.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <param name="directory">The directory.</param>
    /// <returns>The file path.</returns>
    internal static string WriteFile(byte[] bytes, string directory)
    {
        var path = Path.Combine(directory, $"{Guid.NewGuid():N}.pdf");
        File.WriteAllBytes(path, bytes);
        return path;
    }

    /// <summary>Copies bytes one place into a larger array, so the memory does not cover a whole array and is read in windows.</summary>
    /// <param name="bytes">The bytes.</param>
    /// <returns>The memory.</returns>
    private static ReadOnlyMemory<byte> Offset(byte[] bytes)
    {
        var padded = new byte[bytes.Length + 1];
        bytes.CopyTo(padded, 1);
        return padded.AsMemory(1);
    }
}
