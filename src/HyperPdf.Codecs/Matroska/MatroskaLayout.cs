// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Codecs.Matroska;

/// <summary>What is read when a Matroska file opens.</summary>
/// <param name="Tracks">The tracks the library lists.</param>
/// <param name="Scale">The seconds in a timestamp unit.</param>
/// <param name="Clusters">The clusters found.</param>
/// <param name="SegmentStart">The file offset of the segment's content.</param>
/// <param name="SegmentEnd">The file offset after the segment.</param>
/// <param name="Seconds">The length of the file in seconds, or 0.</param>
internal sealed record MatroskaLayout(List<MatroskaTrack> Tracks, double Scale, List<MatroskaCluster> Clusters, long SegmentStart, long SegmentEnd, double Seconds);
