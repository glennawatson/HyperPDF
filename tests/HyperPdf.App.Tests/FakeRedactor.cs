// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Redaction;

namespace HyperPdf.App.Tests;

/// <summary>A redactor that writes a fixed file, and can be held up or made to fail.</summary>
internal sealed class FakeRedactor : IDocumentRedactor
{
    /// <summary>The areas in the default report.</summary>
    private const int ReportedAreas = 3;

    /// <summary>The pages in the default report.</summary>
    private const int ReportedPages = 2;

    /// <summary>The characters in the default report.</summary>
    private const int ReportedCharacters = 120;

    /// <summary>The bytes the fake writes.</summary>
    private static readonly byte[] Output = "%PDF-1.7 fake redacted copy"u8.ToArray();

    /// <inheritdoc/>
    public IDocument Document => throw new NotSupportedException("The fake belongs to no document.");

    /// <summary>Gets how many times the redactor ran.</summary>
    internal int Calls { get; private set; }

    /// <summary>Gets the settings of the last run.</summary>
    internal RedactionSettings? Received { get; private set; }

    /// <summary>Gets or sets the report returned.</summary>
    internal RedactionReport Report { get; set; } = new(ReportedAreas, ReportedPages, ReportedCharacters, 1, 0, ReportedPages);

    /// <summary>Gets or sets what the run throws after writing, or <see langword="null"/>.</summary>
    internal Exception? Failure { get; set; }

    /// <summary>Gets or sets a gate the run waits on after writing; <see langword="null"/> runs straight through.</summary>
    internal TaskCompletionSource? Gate { get; set; }

    /// <summary>Gets the signal that the run has written and is waiting at the gate.</summary>
    internal TaskCompletionSource Started { get; } = new(TaskCreationOptions.RunContinuationsAsynchronously);

    /// <inheritdoc/>
    public async Task<RedactionReport> ApplyAsync(Stream destination, RedactionSettings settings, CancellationToken cancellationToken)
    {
        Calls++;
        Received = settings;
        await destination.WriteAsync(Output, cancellationToken).ConfigureAwait(false);
        _ = Started.TrySetResult();
        if (Gate is { } gate)
        {
            await gate.Task.WaitAsync(cancellationToken).ConfigureAwait(false);
        }

        return Failure is null ? Report : throw Failure;
    }
}
