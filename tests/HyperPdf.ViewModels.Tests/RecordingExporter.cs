// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Printing;

namespace HyperPdf.ViewModels.Tests;

/// <summary>A page exporter that passes the call to the real one, records the thread it ran on, and can be held at the engine's export gate.</summary>
/// <param name="inner">The real exporter.</param>
/// <param name="document">The document the feature belongs to.</param>
/// <param name="engine">The engine that keeps the record.</param>
internal sealed class RecordingExporter(IPageExporter inner, IDocument document, PausingEngine engine) : IPageExporter
{
    /// <summary>The longest a held export waits for its release on a pool thread; the call cannot be awaited.</summary>
    private static readonly TimeSpan HoldLimit = TimeSpan.FromSeconds(30);

    /// <inheritdoc/>
    public IDocument Document => document;

    /// <inheritdoc/>
    public bool ExportPages(ReadOnlySpan<int> pages, in SheetLayout layout, Stream destination)
    {
        engine.Calls.Enqueue(new(nameof(ExportPages), Environment.CurrentManagedThreadId));
        var gate = Hold();
        var written = inner.ExportPages(pages, layout, destination);
        if (gate is not null && destination is FileStream file)
        {
            gate.Exported.Enqueue(file.Name);
            _ = gate.Found.TrySetResult();
        }

        return written;
    }

    /// <summary>Holds the calling thread at the export gate while one is armed.</summary>
    /// <returns>The gate that held the call, or <see langword="null"/> when none was armed.</returns>
    private PausingEngine.PauseGate? Hold()
    {
        if (engine.Gate is not { Point: PausingEngine.PausePoint.Export } gate)
        {
            return null;
        }

        _ = gate.Reached.TrySetResult();

        // The call is synchronous, so the hold on a pool thread polls the release with a bound.
        _ = SpinWait.SpinUntil(() => gate.Released.Task.IsCompleted, HoldLimit);
        return gate;
    }
}
