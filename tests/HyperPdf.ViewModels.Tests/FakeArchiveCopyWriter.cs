// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Archiving;
using HyperPdf.Core.Documents;

namespace HyperPdf.ViewModels.Tests;

/// <summary>A PDF/A copy writer that writes a fixed file, refuses on request, and can be held up or made to fail.</summary>
internal sealed class FakeArchiveCopyWriter : IArchiveCopyWriter
{
    /// <summary>The bytes the fake writes when the copy is made.</summary>
    private static readonly byte[] Output = "%PDF-1.7 fake PDF/A copy"u8.ToArray();

    /// <inheritdoc/>
    public IDocument Document => throw new NotSupportedException("The fake belongs to no document.");

    /// <summary>Gets how many times the writer ran.</summary>
    internal int Calls { get; private set; }

    /// <summary>Gets the level of the last run.</summary>
    internal ArchiveLevel? Received { get; private set; }

    /// <summary>Gets or sets the changes reported for a made copy.</summary>
    internal IReadOnlyList<ArchiveNote> Changes { get; set; } = [new("Added a colour profile.", "6.2.3")];

    /// <summary>Gets or sets the problems that stop the copy; empty makes it.</summary>
    internal IReadOnlyList<ArchiveNote> Problems { get; set; } = [];

    /// <summary>Gets or sets what the run throws, or <see langword="null"/>.</summary>
    internal Exception? Failure { get; set; }

    /// <summary>Gets or sets a gate the run waits on before it finishes; <see langword="null"/> runs straight through.</summary>
    internal TaskCompletionSource? Gate { get; set; }

    /// <summary>Gets the signal that the run has started and is waiting at the gate.</summary>
    internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc/>
    public async Task<ArchiveCopyResult> WriteArchiveCopyAsync(Stream destination, ArchiveLevel level, CancellationToken cancellationToken)
    {
        Calls++;
        Received = level;
        _ = Started.TrySetResult();
        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        if (Failure is { } failure)
        {
            throw failure;
        }

        if (Problems.Count == 0)
        {
            await destination.WriteAsync(Output, cancellationToken).ConfigureAwait(false);
        }

        return new(level, Problems.Count == 0 ? Changes : [], Problems);
    }
}
