// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine.Tests;

/// <summary>Checks the counted lifetime of page-access locks.</summary>
public sealed class HyperPdfPageLeaseTests
{
    /// <summary>The marker that stops new leases.</summary>
    private const int Closed = int.MinValue;

    /// <summary>Close defers native lock disposal until its final read or write lease leaves.</summary>
    /// <param name="write">Whether the held lease is a writer.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(false)]
    [Arguments(true)]
    public async Task CloseDefersDisposalUntilFinalLeaseLeaves(bool write)
    {
        var result = ProbeHeldLease(write);
        await Assert.That(result.ClosedLeaseCount).IsEqualTo(Closed + 1);
        await Assert.That(result.CountAfterRejectedEntry).IsEqualTo(Closed + 1);
        await Assert.That(result.DisposedWhileHeld).IsFalse();
        await Assert.That(result.FinalLeaseCount).IsEqualTo(Closed);
        await Assert.That(result.DisposedAfterRelease).IsTrue();
    }

    /// <summary>A failed recursive entry releases its registered lifetime lease.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FailedRecursiveEntryDoesNotRetainLease()
    {
        var result = ProbeFailedEntry();
        await Assert.That(result.RecursionRejected).IsTrue();
        await Assert.That(result.CountAfterFailure).IsEqualTo(1);
        await Assert.That(result.CountAfterRelease).IsEqualTo(0);
        await Assert.That(result.DisposedAfterClose).IsTrue();
    }

    /// <summary>An unused scope and repeated close calls have no duplicate cleanup effect.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task DefaultScopeAndRepeatedCloseAreSafe() => await Assert.That(ProbeEmptyClose()).IsTrue();

    /// <summary>Exercises held scopes entirely synchronously on the acquiring thread.</summary>
    /// <param name="write">Whether the held scope excludes readers.</param>
    /// <returns>The observed lock lifetime and lease counts.</returns>
    private static HeldResult ProbeHeldLease(bool write)
    {
        var gate = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
        var leases = 0;
        int closedCount;
        int rejectedCount;
        bool disposedWhileHeld;
        using (var held = new HyperPdfPageAccess(gate, ref leases, write))
        {
            HyperPdfPageAccess.Close(gate, ref leases);
            closedCount = leases;
            disposedWhileHeld = IsDisposed(gate);
            using var rejected = new HyperPdfPageAccess(gate, ref leases, write);
            rejectedCount = leases;
        }

        HyperPdfPageAccess.Close(gate, ref leases);
        return new(closedCount, rejectedCount, disposedWhileHeld, leases, IsDisposed(gate));
    }

    /// <summary>Checks that constructor failure unwinds registration before the original scope exits.</summary>
    /// <returns>The rejection and counts before and after release.</returns>
    private static FailedResult ProbeFailedEntry()
    {
        var gate = new ReaderWriterLockSlim(LockRecursionPolicy.NoRecursion);
        var leases = 0;
        var rejected = false;
        int countAfterFailure;
        using (var held = new HyperPdfPageAccess(gate, ref leases, false))
        {
            try
            {
                using var recursive = new HyperPdfPageAccess(gate, ref leases, false);
            }
            catch (LockRecursionException)
            {
                rejected = true;
            }

            countAfterFailure = leases;
        }

        var countAfterRelease = leases;
        HyperPdfPageAccess.Close(gate, ref leases);
        return new(rejected, countAfterFailure, countAfterRelease, IsDisposed(gate));
    }

    /// <summary>Closes an idle lock twice and disposes a default access scope.</summary>
    /// <returns>Whether the lock is disposed.</returns>
    private static bool ProbeEmptyClose()
    {
        using var empty = default(HyperPdfPageAccess);
        var gate = new ReaderWriterLockSlim();
        var leases = 0;
        HyperPdfPageAccess.Close(gate, ref leases);
        HyperPdfPageAccess.Close(gate, ref leases);
        using var rejected = new HyperPdfPageAccess(gate, ref leases, false);
        return leases == Closed && IsDisposed(gate);
    }

    /// <summary>Probes disposal without waiting for another lock holder.</summary>
    /// <param name="gate">The lock whose lifetime is checked.</param>
    /// <returns>Whether entering the lock is rejected as disposed.</returns>
    private static bool IsDisposed(ReaderWriterLockSlim gate)
    {
        try
        {
            if (gate.TryEnterReadLock(0))
            {
                gate.ExitReadLock();
            }

            return false;
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
    }

    /// <summary>The synchronous held-scope observations.</summary>
    /// <param name="ClosedLeaseCount">The count immediately after close.</param>
    /// <param name="CountAfterRejectedEntry">The count after a closed entry attempt.</param>
    /// <param name="DisposedWhileHeld">Whether the lock was disposed prematurely.</param>
    /// <param name="FinalLeaseCount">The state after release.</param>
    /// <param name="DisposedAfterRelease">Whether final release disposed the lock.</param>
    private readonly record struct HeldResult(int ClosedLeaseCount, int CountAfterRejectedEntry, bool DisposedWhileHeld, int FinalLeaseCount, bool DisposedAfterRelease);

    /// <summary>The synchronous failed-entry observations.</summary>
    /// <param name="RecursionRejected">Whether the second entry failed.</param>
    /// <param name="CountAfterFailure">The count retaining only the original entry.</param>
    /// <param name="CountAfterRelease">The count after that entry leaves.</param>
    /// <param name="DisposedAfterClose">Whether the idle close disposed the lock.</param>
    private readonly record struct FailedResult(bool RecursionRejected, int CountAfterFailure, int CountAfterRelease, bool DisposedAfterClose);
}
