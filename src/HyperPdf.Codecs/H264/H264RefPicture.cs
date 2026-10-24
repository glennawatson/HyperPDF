// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Codecs.H264;

/// <summary>A decoded frame kept as a reference for later pictures, with how it is marked (clause 8.2.5 of ITU-T H.264).</summary>
[DebuggerDisplay("H264RefPicture: frame {FrameNum}, long term {IsLongTerm}")]
internal sealed class H264RefPicture
{
    /// <summary>Initializes a new instance of the <see cref="H264RefPicture"/> class.</summary>
    /// <param name="picture">The decoded frame.</param>
    /// <param name="frameNum">The frame_num of the frame.</param>
    internal H264RefPicture(H264Picture picture, int frameNum)
    {
        Picture = picture;
        FrameNum = frameNum;
        FrameNumWrap = frameNum;
    }

    /// <summary>Gets the decoded frame.</summary>
    internal H264Picture Picture { get; }

    /// <summary>Gets or sets the frame_num of the frame.</summary>
    internal int FrameNum { get; set; }

    /// <summary>Gets or sets the frame number made relative to the frame being decoded, so it counts up through wraps.</summary>
    internal int FrameNumWrap { get; set; }

    /// <summary>Gets or sets a value indicating whether the frame is a long-term reference.</summary>
    internal bool IsLongTerm { get; set; }

    /// <summary>Gets or sets the long-term frame index when the frame is a long-term reference.</summary>
    internal int LongTermFrameIdx { get; set; }
}
