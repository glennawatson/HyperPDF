// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Syntax;

/// <summary>Checks perfect-hash exact matches and concurrent publication of document-specific names.</summary>
public sealed class PdfNameTableTests
{
    /// <summary>The concurrent interning workers.</summary>
    private const int Workers = 32;

    /// <summary>The shared names repeatedly interned by every worker.</summary>
    private const int SharedNames = 64;

    /// <summary>The names introduced independently by each worker.</summary>
    private const int PrivateNames = 64;

    /// <summary>The duplicate lookup rounds.</summary>
    private const int Repetitions = 4;

    /// <summary>The bytes sampled at each spelling edge.</summary>
    private const int SampleWordBytes = 4;

    /// <summary>The mask making a middle byte differ from the ASCII known spelling.</summary>
    private const byte NonAsciiMask = 0x80;

    /// <summary>The lookup rounds warming the allocation probe.</summary>
    private const int WarmupCalls = 128;

    /// <summary>The lookup rounds measured by the allocation probe.</summary>
    private const int MeasuredCalls = 1024;

    /// <summary>The hang bound for concurrent name publication.</summary>
    private static readonly TimeSpan ConcurrentLimit = TimeSpan.FromMinutes(1);

    /// <summary>Every fixed name maps to its own slot and round-trips through the single spelling blob.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task KnownNamesHaveDistinctSlotsAndExactSpellings()
    {
        var table = new PdfNameTable();
        var slots = new HashSet<int>();
        foreach (var known in Enum.GetValues<KnownName>())
        {
            var spelling = PdfNameTable.GetKnownSpelling(known).ToArray();
            if (known == KnownName.None)
            {
                await Assert.That(PdfNameTable.TryGetKnown(spelling, out _)).IsFalse();
                await Assert.That(spelling).IsEmpty();
                continue;
            }

            await Assert.That(PdfNameTable.TryGetKnown(spelling, out var name)).IsTrue();
            await Assert.That(name.Id).IsEqualTo((int)known);
            await Assert.That(table.Intern(spelling)).IsEqualTo(name);
            await Assert.That(table.GetSpelling(name).SequenceEqual(spelling)).IsTrue();
            var hash = KnownNameHash.Hash(spelling);
            var size = KnownNameSpellings.Count - 1;
            var displacement = KnownNameSpellings.Displacements[(int)(hash % (uint)size)];
            var slot = displacement < 0 ? -displacement - 1 : (int)(KnownNameHash.Displace(hash, displacement) % (uint)size);
            await Assert.That(slots.Add(slot)).IsTrue();
        }

        await Assert.That(slots.Count).IsEqualTo(KnownNameSpellings.Count - 1);
        await Assert.That(table.Count).IsEqualTo(KnownNameSpellings.Count);
    }

    /// <summary>Unknown byte spellings are checked after hashing rather than mistaken for the occupant of their slot.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnknownNamesNeverMatchOccupiedHashSlots()
    {
        foreach (var known in Enum.GetValues<KnownName>())
        {
            var spelling = PdfNameTable.GetKnownSpelling(known).ToArray();
            var unknown = new byte[spelling.Length + 1];
            spelling.CopyTo(unknown, 0);
            await Assert.That(PdfNameTable.TryGetKnown(unknown, out var name)).IsFalse();
            await Assert.That(name).IsEqualTo(default(PdfName));
            if (spelling.Length <= SampleWordBytes + SampleWordBytes)
            {
                continue;
            }

            var knownHash = KnownNameHash.Hash(spelling);
            spelling[SampleWordBytes] ^= NonAsciiMask;
            await Assert.That(KnownNameHash.Hash(spelling)).IsEqualTo(knownHash);
            await Assert.That(PdfNameTable.TryGetKnown(spelling, out _)).IsFalse();
        }
    }

    /// <summary>Concurrent misses assign one id per spelling and publish readable bytes before exposing each id.</summary>
    /// <param name="token">Cancelled when the runner stops the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConcurrentInterningPublishesUniqueIdsAndCompleteSpellings(CancellationToken token)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(token);
        timeout.CancelAfter(ConcurrentLimit);
        var table = new PdfNameTable();
        var gate = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        var tasks = new Task<string[]>[Workers];
        for (var i = 0; i < tasks.Length; i++)
        {
            tasks[i] = InternAfterGateAsync(table, gate.Task, i, timeout.Token);
        }

        gate.SetResult();
        var results = await Task.WhenAll(tasks).WaitAsync(timeout.Token);
        foreach (var problems in results)
        {
            await Assert.That(problems).IsEmpty();
        }

        await Assert.That(table.Count).IsEqualTo(KnownNameSpellings.Count + SharedNames + (Workers * PrivateNames));
        var ids = new HashSet<int>();
        for (var worker = 0; worker < Workers; worker++)
        {
            for (var index = 0; index < PrivateNames; index++)
            {
                await Assert.That(ids.Add(table.Intern(PrivateSpelling(worker, index)).Id)).IsTrue();
            }
        }
    }

    /// <summary>Empty and arbitrary byte names remain document-specific and retain their exact bytes.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task BinaryAndEmptyNamesRoundTrip()
    {
        var table = new PdfNameTable();
        byte[] spelling = [0, 0xFF, 0xC0, 0x2F, 0x23];
        var binary = table.Intern(spelling);
        var empty = table.Intern(ReadOnlySpan<byte>.Empty);
        await Assert.That(table.GetSpelling(binary).SequenceEqual(spelling)).IsTrue();
        await Assert.That(table.GetSpelling(empty).IsEmpty).IsTrue();
        await Assert.That(table.GetSpelling(default).IsEmpty).IsTrue();
        await Assert.That(empty.IsKnown).IsFalse();
        await Assert.That(table.Intern(ReadOnlySpan<byte>.Empty)).IsEqualTo(empty);
        await Assert.That(table.GetSpelling(new(int.MaxValue)).IsEmpty).IsTrue();
    }

    /// <summary>Warm known and document-specific byte lookups allocate no managed objects.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepeatedByteLookupsDoNotAllocate()
    {
        var table = new PdfNameTable();
        var unknown = "DocumentSpecificAllocationProbe"u8.ToArray();
        var name = table.Intern(unknown);
        for (var i = 0; i < WarmupCalls; i++)
        {
            _ = LookupBatch(table, unknown, name);
        }

        var total = 0;
        var allocated = AllocationProbe.Minimum(() =>
        {
            for (var i = 0; i < MeasuredCalls; i++)
            {
                total += LookupBatch(table, unknown, name);
            }
        });
        await Assert.That(total).IsGreaterThan(0);
        await Assert.That(allocated).IsEqualTo(0);
    }

    /// <summary>Starts interning together, mixing duplicate byte and text callers with independent insertions.</summary>
    /// <param name="table">The shared table.</param>
    /// <param name="gate">Releases all workers.</param>
    /// <param name="worker">The worker number.</param>
    /// <param name="token">Cancelled when the runner stops the test.</param>
    /// <returns>The publication or identity problems observed.</returns>
    private static async Task<string[]> InternAfterGateAsync(PdfNameTable table, Task gate, int worker, CancellationToken token)
    {
        await gate.WaitAsync(token);
        return await Task.Factory.StartNew(
            static state =>
            {
                var input = (InternWorker)state!;
                return InternBatch(input.Table, input.Index, input.Token);
            },
            new InternWorker(table, worker, token),
            token,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    /// <summary>Interns repeated shared spellings and independent names on a dedicated worker thread.</summary>
    /// <param name="table">The shared table.</param>
    /// <param name="worker">The worker number.</param>
    /// <param name="token">Cancelled when the runner stops the test.</param>
    /// <returns>The publication or identity problems observed.</returns>
    private static string[] InternBatch(PdfNameTable table, int worker, CancellationToken token)
    {
        var problems = new List<string>();
        for (var round = 0; round < Repetitions; round++)
        {
            token.ThrowIfCancellationRequested();
            for (var i = 0; i < SharedNames; i++)
            {
                CheckSpelling(table, string.Create(CultureInfo.InvariantCulture, $"SharedDocumentName{i}"), problems);
            }

            for (var i = 0; i < PrivateNames; i++)
            {
                CheckSpelling(table, PrivateSpelling(worker, i), problems);
            }
        }

        return [.. problems];
    }

    /// <summary>Checks bytes immediately after publication and compares byte and text identities.</summary>
    /// <param name="table">The table.</param>
    /// <param name="spelling">The unknown name.</param>
    /// <param name="problems">Receives any torn spelling or duplicate id.</param>
    private static void CheckSpelling(PdfNameTable table, string spelling, List<string> problems)
    {
        var bytes = Encoding.UTF8.GetBytes(spelling);
        var name = table.Intern(bytes);
        if (!table.GetSpelling(name).SequenceEqual(bytes) || table.Intern(spelling) != name || table.Count <= name.Id)
        {
            problems.Add(spelling);
        }
    }

    /// <summary>Builds a spelling unique to one worker and index.</summary>
    /// <param name="worker">The worker number.</param>
    /// <param name="index">The spelling number.</param>
    /// <returns>The document-specific spelling.</returns>
    private static string PrivateSpelling(int worker, int index) => string.Create(CultureInfo.InvariantCulture, $"Worker{worker}DocumentName{index}");

    /// <summary>Reads known and document-specific names and spelling bytes in one allocation probe batch.</summary>
    /// <param name="table">The name table.</param>
    /// <param name="unknown">The existing document-specific bytes.</param>
    /// <param name="name">The existing document-specific name.</param>
    /// <returns>A checksum preventing unused lookup results.</returns>
    private static int LookupBatch(PdfNameTable table, byte[] unknown, PdfName name)
    {
        _ = PdfNameTable.TryGetKnown("Type"u8, out var known);
        return known.Id + table.Intern(unknown).Id + table.GetSpelling(name).Length + table.Count;
    }

    /// <summary>Inputs for one dedicated interning thread.</summary>
    /// <param name="Table">The shared table.</param>
    /// <param name="Index">The worker number.</param>
    /// <param name="Token">Cancelled when the runner stops the test.</param>
    private sealed record InternWorker(PdfNameTable Table, int Index, CancellationToken Token);
}
