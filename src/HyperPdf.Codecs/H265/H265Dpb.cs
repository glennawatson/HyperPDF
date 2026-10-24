// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;

namespace HyperPdf.Codecs.H265;

/// <summary>The decoded picture buffer: the pictures kept for reference or waiting for output, and the bumping process that releases them in output order (C.5.2).</summary>
[DebuggerDisplay("H265Dpb: {Pictures.Count} pictures")]
internal sealed class H265Dpb
{
    /// <summary>Gets the pictures in the buffer.</summary>
    internal List<H265Picture> Pictures { get; } = [];

    /// <summary>Empties the buffer without output.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Clear() => Pictures.Clear();

    /// <summary>Marks every picture as unused for reference.</summary>
    internal void UnmarkAll()
    {
        foreach (var picture in Pictures)
        {
            picture.ReferenceState = 0;
        }
    }

    /// <summary>Takes out the pictures that are neither waiting for output nor used for reference.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void RemoveUnused() =>
        Pictures.RemoveAll(static picture => !picture.NeededForOutput && picture.ReferenceState == 0);

    /// <summary>Counts the pictures waiting for output.</summary>
    /// <returns>The count.</returns>
    internal int CountWaiting()
    {
        var count = 0;
        foreach (var picture in Pictures)
        {
            if (picture.NeededForOutput)
            {
                count++;
            }
        }

        return count;
    }

    /// <summary>Takes the waiting picture that comes first in output order.</summary>
    /// <returns>The picture, or <see langword="null"/> when none waits.</returns>
    internal H265Picture? TakeNext()
    {
        H265Picture? best = null;
        foreach (var picture in Pictures)
        {
            if (picture.NeededForOutput && (best is null || picture.Poc < best.Poc))
            {
                best = picture;
            }
        }

        if (best is not null)
        {
            best.NeededForOutput = false;
        }

        return best;
    }

    /// <summary>Tells whether a waiting picture has waited longer than the sequence allows.</summary>
    /// <param name="ordering">The buffer limits of the sequence.</param>
    /// <returns><see langword="true"/> when a picture must be output now.</returns>
    internal bool LatencyExceeded(H265SubLayerOrdering ordering)
    {
        if (ordering.MaxLatencyIncreasePlus1 == 0)
        {
            return false;
        }

        var limit = ordering.MaxNumReorder + ordering.MaxLatencyIncreasePlus1 - 1;
        foreach (var picture in Pictures)
        {
            if (picture.NeededForOutput && picture.LatencyCount >= limit)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Counts one more decoded picture against every picture that waits for output and comes after the new picture in output order.</summary>
    /// <param name="poc">The picture order count of the picture just decoded.</param>
    internal void AgeFollowing(int poc)
    {
        foreach (var picture in Pictures)
        {
            if (picture.NeededForOutput && picture.Poc > poc)
            {
                picture.LatencyCount++;
            }
        }
    }
}
