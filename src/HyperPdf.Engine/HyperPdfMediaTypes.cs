// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Engine;

/// <summary>Names the media types of the audio and video files a PDF carries.</summary>
internal static class HyperPdfMediaTypes
{
    /// <summary>The media type used when the file's kind is not known.</summary>
    internal const string Unknown = "application/octet-stream";

    /// <summary>The audio and video file extensions, with their media types, in a table that is searched in order.</summary>
    private static readonly Dictionary<string, string> Types = new(StringComparer.OrdinalIgnoreCase)
    {
        [".mp4"] = "video/mp4",
        [".m4v"] = "video/mp4",
        [".mov"] = "video/quicktime",
        [".webm"] = "video/webm",
        [".mkv"] = "video/x-matroska",
        [".avi"] = "video/x-msvideo",
        [".mpg"] = "video/mpeg",
        [".mpeg"] = "video/mpeg",
        [".wmv"] = "video/x-ms-wmv",
        [".ogv"] = "video/ogg",
        [".mp3"] = "audio/mpeg",
        [".m4a"] = "audio/mp4",
        [".aac"] = "audio/aac",
        [".wav"] = "audio/wav",
        [".ogg"] = "audio/ogg",
        [".oga"] = "audio/ogg",
        [".flac"] = "audio/flac",
        [".wma"] = "audio/x-ms-wma",
        [".aif"] = "audio/aiff",
        [".aiff"] = "audio/aiff",
    };

    /// <summary>Finds the media type of a file name.</summary>
    /// <param name="fileName">The file name.</param>
    /// <returns>The media type, or <see langword="null"/> when the extension is not audio or video.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static string? FromFileName(string fileName) => Types.GetValueOrDefault(Path.GetExtension(fileName));

    /// <summary>Finds the usual extension of a media type.</summary>
    /// <param name="contentType">The media type.</param>
    /// <returns>The extension with its dot; empty when the type is not known.</returns>
    internal static string ExtensionOf(string? contentType)
    {
        foreach (var (extension, type) in Types)
        {
            if (string.Equals(type, contentType, StringComparison.OrdinalIgnoreCase))
            {
                return extension;
            }
        }

        return string.Empty;
    }
}
