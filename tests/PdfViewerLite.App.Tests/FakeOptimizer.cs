// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Optimizing;

namespace PdfViewerLite.App.Tests;

/// <summary>An optimiser that writes a fixed file, and can be held up or made to fail.</summary>
internal sealed class FakeOptimizer : IDocumentOptimizer
{
    /// <summary>The size of the source file in the default report.</summary>
    internal const long SourceBytes = 4_194_304;

    /// <summary>The size of the new file in the default report.</summary>
    internal const long NewBytes = 2_097_152;

    /// <summary>The bytes the fake writes.</summary>
    private static readonly byte[] Output = "%PDF-1.7 fake optimised copy"u8.ToArray();

    /// <inheritdoc/>
    public IDocument Document => throw new NotSupportedException("The fake belongs to no document.");

    /// <summary>Gets how many times the optimiser ran.</summary>
    internal int Calls { get; private set; }

    /// <summary>Gets the settings of the last run.</summary>
    internal OptimizeSettings? Received { get; private set; }

    /// <summary>Gets or sets the report returned.</summary>
    internal OptimizeReport Report { get; set; } = new(SourceBytes, NewBytes, OptimizeWriteMode.Rewritten, [], []);

    /// <summary>Gets or sets what the run throws after writing, or <see langword="null"/>.</summary>
    internal Exception? Failure { get; set; }

    /// <summary>Gets or sets a gate the run waits on after writing and reporting; <see langword="null"/> runs straight through.</summary>
    internal TaskCompletionSource? Gate { get; set; }

    /// <summary>Gets the signal that the run has written and is waiting at the gate.</summary>
    internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc/>
    public async Task<OptimizeReport> OptimizeAsync(Stream destination, OptimizeSettings settings, IProgress<OptimizeProgress>? progress, CancellationToken cancellationToken)
    {
        Calls++;
        Received = settings;
        progress?.Report(new(OptimizeStep.Writing, 1, 1));
        await destination.WriteAsync(Output, cancellationToken).ConfigureAwait(false);
        _ = Started.TrySetResult();
        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Failure is null ? Report : throw Failure;
    }
}
