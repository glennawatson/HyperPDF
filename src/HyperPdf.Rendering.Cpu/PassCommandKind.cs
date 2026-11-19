// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Rendering.Cpu;

/// <summary>The kinds of draw a frame pass holds until the frame ends.</summary>
internal enum PassCommandKind
{
    /// <summary>The whole frame is filled with one colour.</summary>
    Clear = 0,

    /// <summary>A rectangle is filled.</summary>
    Fill = 1,

    /// <summary>A picture is drawn.</summary>
    Picture = 2,

    /// <summary>Drawing is limited to a rectangle.</summary>
    SetClip = 3,

    /// <summary>Drawing reaches the whole frame again.</summary>
    ClearClip = 4,

    /// <summary>The opacity of what follows is set.</summary>
    SetOpacity = 5,

    /// <summary>A group whose pixels are kept between frames starts.</summary>
    BeginKept = 6,

    /// <summary>The group whose pixels are kept ends.</summary>
    EndKept = 7,
}
