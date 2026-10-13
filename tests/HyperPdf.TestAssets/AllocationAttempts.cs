// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.TestAssets;

/// <summary>
/// Tracks several attempts at one allocation measurement and keeps the smallest. A test that awaits inside the measured
/// work uses it inline, because a lambda that wraps the awaits becomes a state-machine class in debug builds and
/// allocates inside the window. Use <see cref="AllocationProbe"/> for work without awaits.
/// </summary>
public sealed class AllocationAttempts
{
    /// <summary>The attempts made when the caller names none.</summary>
    private const int DefaultAttempts = 5;

    /// <summary>The most attempts to make.</summary>
    private readonly int _attempts;

    /// <summary>The attempts started so far.</summary>
    private int _started;

    /// <summary>The thread's allocated bytes when the current attempt began.</summary>
    private long _begin;

    /// <summary>Initializes a new instance of the <see cref="AllocationAttempts"/> class that makes five attempts.</summary>
    public AllocationAttempts()
        : this(DefaultAttempts)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="AllocationAttempts"/> class.</summary>
    /// <param name="attempts">The most attempts to make, at least one.</param>
    public AllocationAttempts(int attempts)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(attempts, 1);
        _attempts = attempts;
        Smallest = long.MaxValue;
    }

    /// <summary>Gets the fewest bytes any finished attempt allocated, or <see cref="long.MaxValue"/> before one finishes.</summary>
    public long Smallest { get; private set; }

    /// <summary>Starts the next attempt unless the attempts are used up or one already allocated nothing.</summary>
    /// <returns>True when the caller should run the measured work and then call <see cref="Stop"/>.</returns>
    public bool Next()
    {
        if (_started >= _attempts || Smallest == 0)
        {
            return false;
        }

        _started++;
        _begin = GC.GetAllocatedBytesForCurrentThread();
        return true;
    }

    /// <summary>Ends the current attempt and keeps its allocation when it is the smallest so far.</summary>
    public void Stop() => Smallest = Math.Min(Smallest, GC.GetAllocatedBytesForCurrentThread() - _begin);
}
