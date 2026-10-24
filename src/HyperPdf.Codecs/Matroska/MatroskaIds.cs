// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Matroska;

/// <summary>The element IDs of Matroska and WebM that this reader uses, as the specification writes them.</summary>
internal static class MatroskaIds
{
    /// <summary>The ID of the header that starts every EBML file.</summary>
    internal const uint Header = 0x1A45DFA3;

    /// <summary>The ID of the document type in the header.</summary>
    internal const uint DocType = 0x4282;

    /// <summary>The ID of the segment, which holds everything else.</summary>
    internal const uint Segment = 0x18538067;

    /// <summary>The ID of the segment information.</summary>
    internal const uint Info = 0x1549A966;

    /// <summary>The ID of the number of timestamp units in a millisecond scale, in nanoseconds.</summary>
    internal const uint TimestampScale = 0x2AD7B1;

    /// <summary>The ID of the length of the segment in timestamp units.</summary>
    internal const uint Duration = 0x4489;

    /// <summary>The ID of the list of tracks.</summary>
    internal const uint Tracks = 0x1654AE6B;

    /// <summary>The ID of one track.</summary>
    internal const uint TrackEntry = 0xAE;

    /// <summary>The ID of a track's number.</summary>
    internal const uint TrackNumber = 0xD7;

    /// <summary>The ID of a track's kind.</summary>
    internal const uint TrackType = 0x83;

    /// <summary>The ID of a track's codec name.</summary>
    internal const uint CodecId = 0x86;

    /// <summary>The ID of the bytes a track's decoder is set up with.</summary>
    internal const uint CodecPrivate = 0x63A2;

    /// <summary>The ID of the time a codec needs before the first sound, in nanoseconds.</summary>
    internal const uint CodecDelay = 0x56AA;

    /// <summary>The ID of the default length of a frame, in nanoseconds.</summary>
    internal const uint DefaultDuration = 0x23E383;

    /// <summary>The ID of the picture settings of a track.</summary>
    internal const uint Video = 0xE0;

    /// <summary>The ID of the width of the picture in pixels.</summary>
    internal const uint PixelWidth = 0xB0;

    /// <summary>The ID of the height of the picture in pixels.</summary>
    internal const uint PixelHeight = 0xBA;

    /// <summary>The ID of the sound settings of a track.</summary>
    internal const uint Audio = 0xE1;

    /// <summary>The ID of the sample rate.</summary>
    internal const uint SamplingFrequency = 0xB5;

    /// <summary>The ID of the channel count.</summary>
    internal const uint Channels = 0x9F;

    /// <summary>The ID of the bits in a sample.</summary>
    internal const uint BitDepth = 0x6264;

    /// <summary>The ID of a cluster, a run of blocks that share a start time.</summary>
    internal const uint Cluster = 0x1F43B675;

    /// <summary>The ID of a cluster's start time.</summary>
    internal const uint ClusterTimestamp = 0xE7;

    /// <summary>The ID of a block that stands alone.</summary>
    internal const uint SimpleBlock = 0xA3;

    /// <summary>The ID of a block with extra information.</summary>
    internal const uint BlockGroup = 0xA0;

    /// <summary>The ID of the block inside a block group.</summary>
    internal const uint Block = 0xA1;

    /// <summary>The ID of how long a block lasts, inside a block group.</summary>
    internal const uint BlockDuration = 0x9B;

    /// <summary>The ID of a reference to another block, which a key block does not have.</summary>
    internal const uint ReferenceBlock = 0xFB;

    /// <summary>The ID of the index of the file.</summary>
    internal const uint Cues = 0x1C53BB6B;

    /// <summary>The ID of one entry of the index.</summary>
    internal const uint CuePoint = 0xBB;

    /// <summary>The ID of an index entry's time.</summary>
    internal const uint CueTime = 0xB3;

    /// <summary>The ID of the places an index entry points to.</summary>
    internal const uint CueTrackPositions = 0xB7;

    /// <summary>The ID of the track an index entry is for.</summary>
    internal const uint CueTrack = 0xF7;

    /// <summary>The ID of the offset of the cluster an index entry points to.</summary>
    internal const uint CueClusterPosition = 0xF1;

    /// <summary>The ID of the list that tells where the other top level elements are.</summary>
    internal const uint SeekHead = 0x114D9B74;

    /// <summary>The ID of the tags.</summary>
    internal const uint Tags = 0x1254C367;

    /// <summary>The ID of the chapters.</summary>
    internal const uint Chapters = 0x1043A770;

    /// <summary>The ID of the attachments.</summary>
    internal const uint Attachments = 0x1941A469;
}
