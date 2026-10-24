// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs;

/// <summary>What a track of a media file holds.</summary>
public enum MediaTrackKind
{
    /// <summary>A track of sound.</summary>
    Audio = 0,

    /// <summary>A track of pictures.</summary>
    Video = 1,

    /// <summary>A track of captions or subtitles.</summary>
    Caption = 2,
}
