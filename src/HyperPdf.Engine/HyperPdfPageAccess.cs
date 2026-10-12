// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdf.Engine;

/// <summary>Holds a synchronous page read or edit while allowing concurrent readers.</summary>
[DebuggerDisplay("HyperPdfPageAccess: write {_write}")]
internal readonly ref struct HyperPdfPageAccess
{
    /// <summary>The closed bit in the outstanding lease count.</summary>
    private const int Closed = int.MinValue;

    /// <summary>The page access lock, retained while waiting and holding it.</summary>
    private readonly ReaderWriterLockSlim? _gate;

    /// <summary>The owner's outstanding leases and closed bit.</summary>
    private readonly ref int _leases;

    /// <summary>Whether the scope excludes readers.</summary>
    private readonly bool _write;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfPageAccess"/> struct.</summary>
    /// <param name="gate">The page access lock.</param>
    /// <param name="leases">The owner's outstanding leases and closed bit.</param>
    /// <param name="write">Whether to exclude readers.</param>
    internal HyperPdfPageAccess(ReaderWriterLockSlim gate, ref int leases, bool write)
    {
        _gate = null;
        _leases = ref leases;
        _write = write;
        if (!Register(ref leases))
        {
            return;
        }

        try
        {
            if (write)
            {
                gate.EnterWriteLock();
            }
            else
            {
                gate.EnterReadLock();
            }

            _gate = gate;
        }
        catch
        {
            Release(gate, ref leases);
            throw;
        }
    }

    /// <summary>Gets whether the page access lock was acquired.</summary>
    internal bool IsActive => _gate is not null;

    /// <summary>Stops new leases and disposes the lock after all registered callers leave.</summary>
    /// <param name="gate">The page access lock.</param>
    /// <param name="leases">The owner's outstanding leases and closed bit.</param>
    internal static void Close(ReaderWriterLockSlim gate, ref int leases)
    {
        if (Interlocked.Or(ref leases, Closed) == 0)
        {
            gate.Dispose();
        }
    }

    /// <summary>Releases the page access lock and its lifetime lease.</summary>
    internal void Dispose()
    {
        if (_gate is null)
        {
            return;
        }

        if (_write)
        {
            _gate.ExitWriteLock();
        }
        else
        {
            _gate.ExitReadLock();
        }

        Release(_gate, ref _leases);
    }

    /// <summary>Registers a caller before it waits for the lock.</summary>
    /// <param name="leases">The owner's outstanding leases and closed bit.</param>
    /// <returns>Whether the lock still accepts callers.</returns>
    private static bool Register(ref int leases)
    {
        var current = Volatile.Read(ref leases);
        while (current >= 0)
        {
            var observed = Interlocked.CompareExchange(ref leases, current + 1, current);
            if (observed == current)
            {
                return true;
            }

            current = observed;
        }

        return false;
    }

    /// <summary>Releases a caller and disposes the closed lock when no callers remain.</summary>
    /// <param name="gate">The page access lock.</param>
    /// <param name="leases">The owner's outstanding leases and closed bit.</param>
    private static void Release(ReaderWriterLockSlim gate, ref int leases)
    {
        if (Interlocked.Decrement(ref leases) == Closed)
        {
            gate.Dispose();
        }
    }
}
