// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Archiving;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Flattening;

namespace HyperPdf.ViewModels.Tests;

/// <summary>A flattened copy writer that records its calls and returns what a test sets, so the window can be tested without an engine.</summary>
internal sealed class FakeFlattenCopyWriter : IFlattenCopyWriter
{
    /// <summary>The form fields the fake reports as flattened.</summary>
    private const int FormFields = 3;

    /// <summary>The other annotations the fake reports as flattened.</summary>
    private const int Marks = 2;

    /// <summary>The bytes the fake writes when the copy is made.</summary>
    private static readonly byte[] Output = "%PDF-1.7 fake flattened copy"u8.ToArray();

    /// <inheritdoc/>
    public IDocument Document => throw new NotSupportedException("The fake belongs to no document.");

    /// <summary>Gets how many times the writer ran.</summary>
    internal int Calls { get; private set; }

    /// <summary>Gets the options of the last run.</summary>
    internal FlattenCopyOptions? LastOptions { get; private set; }

    /// <summary>Gets or sets the notes reported for a made copy.</summary>
    internal IReadOnlyList<ArchiveNote> Notes { get; set; } = [new("1 annotation was marked hidden, so it was left as it was.", "12.5.3")];

    /// <summary>Gets or sets the problems that stop the copy; empty makes it.</summary>
    internal IReadOnlyList<ArchiveNote> Problems { get; set; } = [];

    /// <summary>Gets or sets what the run throws, or <see langword="null"/>.</summary>
    internal Exception? Failure { get; set; }

    /// <summary>Gets or sets a gate the run waits on before it finishes; <see langword="null"/> runs straight through.</summary>
    internal TaskCompletionSource? Gate { get; set; }

    /// <summary>Gets the signal that the run has started and is waiting at the gate.</summary>
    internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc/>
    public async Task<FlattenCopyResult> WriteFlattenedCopyAsync(Stream destination, FlattenCopyOptions options, CancellationToken cancellationToken)
    {
        Calls++;
        LastOptions = options;
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
            return new(1, FormFields, Marks, true, Notes, []);
        }

        return new(0, 0, 0, false, [], Problems);
    }
}
