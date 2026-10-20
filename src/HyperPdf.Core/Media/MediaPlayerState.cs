// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Media;

/// <summary>Where a media player is in its life.</summary>
public enum MediaPlayerState
{
    /// <summary>No file is open.</summary>
    Idle = 0,

    /// <summary>A file is open and not playing.</summary>
    Ready = 1,

    /// <summary>The file is playing.</summary>
    Playing = 2,

    /// <summary>The reader paused the file.</summary>
    Paused = 3,

    /// <summary>The file played to its end.</summary>
    Ended = 4,

    /// <summary>The file could not be opened or played.</summary>
    Failed = 5,
}
