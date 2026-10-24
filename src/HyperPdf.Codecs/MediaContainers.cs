// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Codecs.Aac;
using HyperPdf.Codecs.Containers;
using HyperPdf.Codecs.Flac;
using HyperPdf.Codecs.Matroska;
using HyperPdf.Codecs.Mp3;
using HyperPdf.Codecs.Mp4;
using HyperPdf.Codecs.Ogg;

namespace HyperPdf.Codecs;

/// <summary>Opens media files by looking at what they hold, not at their names.</summary>
public static class MediaContainers
{
    /// <summary>Opens a file.</summary>
    /// <param name="path">The full path of the file.</param>
    /// <returns>The container, which owns the file, or <see langword="null"/> when no container this library reads fits it.</returns>
    public static MediaContainer? Open(string path)
    {
        ArgumentException.ThrowIfNullOrEmpty(path);
        var stream = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, 1, FileOptions.RandomAccess);
        var opened = Open(stream);
        if (opened is null)
        {
            stream.Dispose();
        }

        return opened;
    }

    /// <summary>Opens a stream.</summary>
    /// <param name="stream">The file; it must be able to seek.</param>
    /// <returns>The container, which owns the stream, or <see langword="null"/> when no container this library reads fits it. The stream is left open then.</returns>
    public static MediaContainer? Open(Stream stream)
    {
        ArgumentNullException.ThrowIfNull(stream);
        return WaveContainer.TryOpen(stream)
            ?? FlacContainer.TryOpen(stream)
            ?? OggContainer.TryOpen(stream)
            ?? Mp4Container.TryOpen(stream)
            ?? MatroskaContainer.TryOpen(stream)
            ?? AdtsContainer.TryOpen(stream)
            ?? (MediaContainer?)Mp3Container.TryOpen(stream);
    }
}
