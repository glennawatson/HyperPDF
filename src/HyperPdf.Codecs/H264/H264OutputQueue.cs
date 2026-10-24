// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.H264;

/// <summary>Holds decoded frames until they can be given out in picture order count order (the bumping process of clause C.4.5.3 of ITU-T H.264).</summary>
[DebuggerDisplay("H264OutputQueue: {_pending.Count} waiting")]
internal sealed class H264OutputQueue
{
    /// <summary>The frames waiting to be output.</summary>
    private readonly List<H264PendingFrame> _pending = [];

    /// <summary>Takes a decoded frame and gives out the frames that can leave.</summary>
    /// <param name="frame">The frame and its picture order count.</param>
    /// <param name="isIdr">Whether the frame starts a new coded video sequence, so every frame before it leaves first.</param>
    /// <param name="limit">The most frames that may wait after this call.</param>
    /// <param name="output">Receives the frames that leave, in output order.</param>
    internal void Add(H264PendingFrame frame, bool isIdr, int limit, List<VideoFrame> output)
    {
        if (isIdr)
        {
            Flush(output);
        }

        _pending.Add(frame);
        while (_pending.Count > limit)
        {
            ReleaseFirst(output);
        }
    }

    /// <summary>Gives out every waiting frame in picture order count order.</summary>
    /// <param name="output">Receives the frames.</param>
    internal void Flush(List<VideoFrame> output)
    {
        while (_pending.Count > 0)
        {
            ReleaseFirst(output);
        }
    }

    /// <summary>Forgets the waiting frames without giving them out.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Clear() => _pending.Clear();

    /// <summary>Gives out the waiting frame with the smallest picture order count.</summary>
    /// <param name="output">Receives the frame.</param>
    private void ReleaseFirst(List<VideoFrame> output)
    {
        var first = 0;
        for (var index = 1; index < _pending.Count; index++)
        {
            if (_pending[index].Poc < _pending[first].Poc)
            {
                first = index;
            }
        }

        output.Add(_pending[first].Frame);
        _pending.RemoveAt(first);
    }
}
