// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace PdfViewerLite.App.ViewModels;

/// <summary>The last write time and length used to recognize an external file change.</summary>
/// <param name="Written">The file's last write time in UTC.</param>
/// <param name="Length">The file's length in bytes.</param>
internal readonly record struct DocumentFileStamp(DateTime Written, long Length)
{
    /// <summary>Reads a file's current stamp, or the default when the file is missing.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The current stamp.</returns>
    internal static DocumentFileStamp Read(string path)
    {
        var file = new FileInfo(path);
        return file.Exists ? new(file.LastWriteTimeUtc, file.Length) : default;
    }
}
