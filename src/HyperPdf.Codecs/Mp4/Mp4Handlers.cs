// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Mp4;

/// <summary>The handler types that say what a track holds.</summary>
internal static class Mp4Handlers
{
    /// <summary>Gets the handler type of sound.</summary>
    internal static uint Sound { get; } = Mp4Boxes.Type("soun"u8);

    /// <summary>Gets the handler type of pictures.</summary>
    internal static uint Video { get; } = Mp4Boxes.Type("vide"u8);

    /// <summary>Gets the handler type of timed text.</summary>
    internal static uint Text { get; } = Mp4Boxes.Type("text"u8);

    /// <summary>Gets the handler type of subtitles.</summary>
    internal static uint Subtitle { get; } = Mp4Boxes.Type("sbtl"u8);

    /// <summary>Gets the other handler type of subtitles.</summary>
    internal static uint SubtitleAlt { get; } = Mp4Boxes.Type("subt"u8);
}
