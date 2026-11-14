// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Concurrent;
using System.Diagnostics;
using HyperPdf.Windowing.Windows;

namespace HyperPdf.Viewer;

/// <summary>
/// Runs posted callbacks on the thread that pumps the window, so view model work and awaits come back to it. Posting wakes the
/// window's wait for events instead of the loop polling for work.
/// </summary>
[DebuggerDisplay("WindowThreadContext: {_queue.Count} waiting")]
public sealed class WindowThreadContext : SynchronizationContext
{
    /// <summary>The callbacks waiting to run.</summary>
    private readonly ConcurrentQueue<PostedWork> _queue = new();

    /// <summary>The window whose wait a post ends.</summary>
    private readonly IPlatformWindow _window;

    /// <summary>The thread that pumps the window.</summary>
    private readonly int _thread = Environment.CurrentManagedThreadId;

    /// <summary>Initializes a new instance of the <see cref="WindowThreadContext"/> class on the thread that will pump the window.</summary>
    /// <param name="window">The window.</param>
    public WindowThreadContext(IPlatformWindow window)
    {
        ArgumentNullException.ThrowIfNull(window);
        _window = window;
    }

    /// <summary>Gets a value indicating whether callbacks are waiting.</summary>
    public bool HasWork => !_queue.IsEmpty;

    /// <inheritdoc/>
    public override void Post(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);
        _queue.Enqueue(new(d, state));
        _window.Wake();
    }

    /// <inheritdoc/>
    public override void Send(SendOrPostCallback d, object? state)
    {
        ArgumentNullException.ThrowIfNull(d);
        if (Environment.CurrentManagedThreadId != _thread)
        {
            throw new NotSupportedException("The window thread does not take blocking calls from other threads. Post the work instead.");
        }

        d(state);
    }

    /// <inheritdoc/>
    public override SynchronizationContext CreateCopy() => this;

    /// <summary>Runs every callback that is waiting now. Callbacks posted while it runs wait for the next call, so drawing is never starved.</summary>
    public void Drain()
    {
        for (var remaining = _queue.Count; remaining > 0 && _queue.TryDequeue(out var work); remaining--)
        {
            work.Callback(work.State);
        }
    }
}
