// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Codecs.Containers;

namespace HyperPdf.Codecs.Matroska;

/// <summary>Collects what the top level elements of a segment say while the file is scanned, then builds the layout.</summary>
[DebuggerDisplay("MatroskaLayoutBuilder: {_clusters.Count} clusters")]
internal sealed class MatroskaLayoutBuilder
{
    /// <summary>The bytes at the start of a cluster searched for its start time, which may follow a checksum.</summary>
    private const int ClusterHead = 64;

    /// <summary>The most bytes of a small top level element, such as the tracks, that are read.</summary>
    private const int MaxSmallElement = 16 << 20;

    /// <summary>The nanoseconds in a second.</summary>
    private const double NanosecondsPerSecond = 1e9;

    /// <summary>The nanoseconds in a timestamp unit when the file does not say.</summary>
    private const double DefaultScale = 1_000_000;

    /// <summary>The file offset of the segment's content.</summary>
    private readonly long _segmentStart;

    /// <summary>The file offset after the segment.</summary>
    private readonly long _segmentEnd;

    /// <summary>The offset and start time in timestamp units of each cluster.</summary>
    private readonly List<MatroskaClusterTicks> _clusters = [];

    /// <summary>The tracks.</summary>
    private List<MatroskaTrack> _tracks = [];

    /// <summary>The nanoseconds in a timestamp unit.</summary>
    private double _scaleNanoseconds = DefaultScale;

    /// <summary>The length of the segment in timestamp units, or 0.</summary>
    private double _durationTicks;

    /// <summary>Initializes a new instance of the <see cref="MatroskaLayoutBuilder"/> class.</summary>
    /// <param name="segmentStart">The file offset of the segment's content.</param>
    /// <param name="segmentEnd">The file offset after the segment.</param>
    internal MatroskaLayoutBuilder(long segmentStart, long segmentEnd)
    {
        _segmentStart = segmentStart;
        _segmentEnd = segmentEnd;
    }

    /// <summary>Takes in one top level element of the segment.</summary>
    /// <param name="window">The file.</param>
    /// <param name="element">The element.</param>
    internal void Add(FileWindow window, in MatroskaHeader element)
    {
        switch (element.Id)
        {
            case MatroskaIds.Info:
            {
                ReadInfo(window, element);
                break;
            }

            case MatroskaIds.Tracks:
            {
                var bytes = ReadSmall(window, element);
                _tracks = MatroskaTracks.Read(bytes, new(MatroskaIds.Tracks, 0, bytes.Length));
                break;
            }

            case MatroskaIds.Cluster when element.End != Ebml.UnknownSize:
            {
                AddCluster(window, element);
                break;
            }
        }
    }

    /// <summary>Builds the layout.</summary>
    /// <returns>The layout, or <see langword="null"/> when no track was found.</returns>
    internal MatroskaLayout? Build()
    {
        if (_tracks.Count == 0)
        {
            return null;
        }

        var scale = _scaleNanoseconds / NanosecondsPerSecond;
        var clusters = new List<MatroskaCluster>(_clusters.Count);
        foreach (var cluster in _clusters)
        {
            clusters.Add(new(cluster.Offset, cluster.Ticks * scale));
        }

        var seconds = clusters.Count > 0 ? clusters[^1].Seconds : 0;
        seconds = _durationTicks > 0 ? _durationTicks * scale : seconds;
        return new(_tracks, scale, clusters, _segmentStart, _segmentEnd, seconds);
    }

    /// <summary>Reads a small element's content into a new array.</summary>
    /// <param name="window">The file.</param>
    /// <param name="element">The element.</param>
    /// <returns>The content.</returns>
    /// <exception cref="InvalidDataException">The element is too big or its size is not known.</exception>
    private static byte[] ReadSmall(FileWindow window, in MatroskaHeader element)
    {
        if (element.End == Ebml.UnknownSize || element.End - element.PayloadStart > MaxSmallElement)
        {
            throw new InvalidDataException("A Matroska element is too big or has no size.");
        }

        var length = (int)(element.End - element.PayloadStart);
        return window.Get(element.PayloadStart, length)[..length].ToArray();
    }

    /// <summary>Reads the segment information.</summary>
    /// <param name="window">The file.</param>
    /// <param name="element">The segment information element.</param>
    private void ReadInfo(FileWindow window, in MatroskaHeader element)
    {
        var bytes = ReadSmall(window, element);
        var position = 0;
        while (Ebml.Next(bytes, ref position, bytes.Length, out var child))
        {
            var content = bytes.AsSpan(child.Start, child.End - child.Start);
            _scaleNanoseconds = child.Id == MatroskaIds.TimestampScale ? Ebml.ReadUnsigned(content) : _scaleNanoseconds;
            _durationTicks = child.Id == MatroskaIds.Duration ? Ebml.ReadFloat(content) : _durationTicks;
        }
    }

    /// <summary>Records a cluster and its start time, which is its first child.</summary>
    /// <param name="window">The file.</param>
    /// <param name="element">The cluster element.</param>
    private void AddCluster(FileWindow window, in MatroskaHeader element)
    {
        var bytes = window.Get(element.PayloadStart, ClusterHead);
        var limit = Math.Min(bytes.Length, ClusterHead);
        var position = 0;
        while (Ebml.Next(bytes, ref position, limit, out var child))
        {
            if (child.Id != MatroskaIds.ClusterTimestamp)
            {
                continue;
            }

            _clusters.Add(new(element.Offset, Ebml.ReadUnsigned(bytes[child.Start..child.End])));
            return;
        }
    }
}
