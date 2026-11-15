// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Head.Linux.Wayland;

namespace HyperPdf.Head.Linux;

/// <summary>Lets a window wait for its display connection without spinning, and lets any thread end that wait early.</summary>
[DebuggerDisplay("EventWake")]
internal sealed unsafe class EventWake : IDisposable
{
    /// <summary>The eventfd flag that closes the descriptor in child processes.</summary>
    private const int CloseOnExec = 0x80000;

    /// <summary>The eventfd flag that makes reads return at once when the counter is zero.</summary>
    private const int NonBlocking = 0x800;

    /// <summary>The poll event bit for readable data.</summary>
    private const short Readable = 1;

    /// <summary>The size of one pollfd.</summary>
    private const int PollEntrySize = 8;

    /// <summary>The number of descriptors waited on: the display connection and the wake-up.</summary>
    private const int PollEntries = 2;

    /// <summary>The offset of the wanted events in a pollfd.</summary>
    private const int EventsOffset = 4;

    /// <summary>The offset of the returned events in a pollfd.</summary>
    private const int ReturnedOffset = 6;

    /// <summary>The size of the eventfd counter.</summary>
    private const int CounterSize = sizeof(ulong);

    /// <summary>The wake-up descriptor.</summary>
    private readonly int _descriptor;

    /// <summary>One once the descriptor is closed.</summary>
    private int _disposed;

    /// <summary>Initializes a new instance of the <see cref="EventWake"/> class.</summary>
    /// <exception cref="IOException">The kernel could not make the wake-up descriptor.</exception>
    internal EventWake()
    {
        _descriptor = NativeMethods.EventDescriptor(0, CloseOnExec | NonBlocking);
        if (_descriptor < 0)
        {
            throw new IOException("The wake-up descriptor could not be made.");
        }
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        if (Interlocked.Exchange(ref _disposed, 1) == 0)
        {
            _ = NativeMethods.Close(_descriptor);
        }
    }

    /// <summary>Ends a wait that is running now, or the next one. Safe from any thread.</summary>
    internal void Signal()
    {
        if (Volatile.Read(ref _disposed) != 0)
        {
            return;
        }

        ulong one = 1;
        _ = NativeMethods.Write(_descriptor, (byte*)&one, CounterSize);
    }

    /// <summary>Waits until the display connection has data, a wake-up arrives or the time runs out.</summary>
    /// <param name="connection">The descriptor of the display connection.</param>
    /// <param name="timeoutMilliseconds">The longest wait, or -1 for no limit.</param>
    /// <returns><see langword="true"/> when the display connection has data to read.</returns>
    internal bool Wait(int connection, int timeoutMilliseconds)
    {
        byte* entries = stackalloc byte[PollEntrySize * PollEntries];
        *(int*)entries = connection;
        *(short*)(entries + EventsOffset) = Readable;
        *(short*)(entries + ReturnedOffset) = 0;
        var wake = entries + PollEntrySize;
        *(int*)wake = _descriptor;
        *(short*)(wake + EventsOffset) = Readable;
        *(short*)(wake + ReturnedOffset) = 0;
        if (NativeMethods.Poll(entries, PollEntries, timeoutMilliseconds) <= 0)
        {
            return false;
        }

        if ((*(short*)(wake + ReturnedOffset) & Readable) != 0)
        {
            // Reading resets the counter, so one read answers every wake-up sent so far.
            ulong counter;
            _ = NativeMethods.Read(_descriptor, (byte*)&counter, CounterSize);
        }

        return (*(short*)(entries + ReturnedOffset) & Readable) != 0;
    }
}
