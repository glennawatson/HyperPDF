// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Tests.Rendering;

/// <summary>
/// A time provider whose timers hold until a test releases them, so a delay is an event the test controls and not a duration it
/// waits out. Once released, every timer, old or new, fires as soon as it exists.
/// </summary>
internal sealed class ManualTimeProvider : TimeProvider
{
    /// <summary>The timers created while held.</summary>
    private readonly List<ManualTimer> _held = [];

    /// <summary>Whether timers fire as they are created.</summary>
    private bool _released;

    /// <inheritdoc/>
    public override ITimer CreateTimer(TimerCallback callback, object? state, TimeSpan dueTime, TimeSpan period)
    {
        var timer = new ManualTimer(callback, state);
        bool fireNow;
        lock (_held)
        {
            fireNow = _released;
            if (!fireNow)
            {
                _held.Add(timer);
            }
        }

        if (fireNow)
        {
            FireAway(timer);
        }

        return timer;
    }

    /// <summary>Fires the timers held so far, and every timer created from now on as soon as it exists.</summary>
    internal void Release()
    {
        ManualTimer[] due;
        lock (_held)
        {
            _released = true;
            due = [.. _held];
            _held.Clear();
        }

        foreach (var timer in due)
        {
            FireAway(timer);
        }
    }

    /// <summary>Fires a timer from the pool, as a real timer would, so the creating call has returned first.</summary>
    /// <param name="timer">The timer.</param>
    private static void FireAway(ManualTimer timer) => _ = ThreadPool.UnsafeQueueUserWorkItem(static due => due.Fire(), timer, false);

    /// <summary>A timer that runs its callback when fired by the provider, unless it was disposed first.</summary>
    /// <param name="callback">The callback.</param>
    /// <param name="state">The callback's state.</param>
    private sealed class ManualTimer(TimerCallback callback, object? state) : ITimer
    {
        /// <summary>Whether the timer was disposed, which cancels it.</summary>
        private volatile bool _disposed;

        /// <inheritdoc/>
        public bool Change(TimeSpan dueTime, TimeSpan period) => !_disposed;

        /// <inheritdoc/>
        public void Dispose() => _disposed = true;

        /// <inheritdoc/>
        public ValueTask DisposeAsync()
        {
            _disposed = true;
            return ValueTask.CompletedTask;
        }

        /// <summary>Runs the callback unless the timer was disposed.</summary>
        internal void Fire()
        {
            if (!_disposed)
            {
                callback(state);
            }
        }
    }
}
