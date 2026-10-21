// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Page management that passes every call to the real one and records the thread it ran on.</summary>
/// <param name="inner">The real page management.</param>
/// <param name="document">The document the feature belongs to.</param>
/// <param name="engine">The engine that keeps the record.</param>
internal sealed class RecordingPageManagement(IPageManagementSource inner, IDocument document, PausingEngine engine) : IPageManagementSource
{
    /// <inheritdoc/>
    public IDocument Document => document;

    /// <inheritdoc/>
    public IPageManager PageManager { get; } = new RecordingPageManager(inner.PageManager, engine);

    /// <summary>A page manager that records the thread of each operation.</summary>
    /// <param name="inner">The real manager.</param>
    /// <param name="engine">The engine that keeps the record.</param>
    private sealed class RecordingPageManager(IPageManager inner, PausingEngine engine) : IPageManager
    {
        /// <inheritdoc/>
        public string? UndoLabel => inner.UndoLabel;

        /// <inheritdoc/>
        public string? RedoLabel => inner.RedoLabel;

        /// <inheritdoc/>
        public ValueTask ApplyAsync(PageEditRequest request, CancellationToken cancellationToken)
        {
            engine.Calls.Enqueue(new(nameof(ApplyAsync), Environment.CurrentManagedThreadId));
            return inner.ApplyAsync(request, cancellationToken);
        }

        /// <inheritdoc/>
        public ValueTask ApplyAsync(PageEditRequest request, PageEditReport report, CancellationToken cancellationToken)
        {
            engine.Calls.Enqueue(new(nameof(ApplyAsync), Environment.CurrentManagedThreadId));
            return inner.ApplyAsync(request, report, cancellationToken);
        }

        /// <inheritdoc/>
        public ValueTask InsertAsync(int index, string[] files, CancellationToken cancellationToken)
        {
            engine.Calls.Enqueue(new(nameof(InsertAsync), Environment.CurrentManagedThreadId));
            return inner.InsertAsync(index, files, cancellationToken);
        }

        /// <inheritdoc/>
        public ValueTask InsertAsync(int index, string[] files, PageEditReport report, CancellationToken cancellationToken)
        {
            engine.Calls.Enqueue(new(nameof(InsertAsync), Environment.CurrentManagedThreadId));
            return inner.InsertAsync(index, files, report, cancellationToken);
        }

        /// <inheritdoc/>
        public ValueTask ExtractAsync(ReadOnlyMemory<int> pages, Stream destination, CancellationToken cancellationToken)
        {
            engine.Calls.Enqueue(new(nameof(ExtractAsync), Environment.CurrentManagedThreadId));
            return inner.ExtractAsync(pages, destination, cancellationToken);
        }

        /// <inheritdoc/>
        public ValueTask ExtractAsync(ReadOnlyMemory<int> pages, Stream destination, PageEditReport report, CancellationToken cancellationToken)
        {
            engine.Calls.Enqueue(new(nameof(ExtractAsync), Environment.CurrentManagedThreadId));
            return inner.ExtractAsync(pages, destination, report, cancellationToken);
        }

        /// <inheritdoc/>
        public ValueTask<bool> UndoAsync(CancellationToken cancellationToken)
        {
            engine.Calls.Enqueue(new(nameof(UndoAsync), Environment.CurrentManagedThreadId));
            return inner.UndoAsync(cancellationToken);
        }

        /// <inheritdoc/>
        public ValueTask<bool> RedoAsync(CancellationToken cancellationToken)
        {
            engine.Calls.Enqueue(new(nameof(RedoAsync), Environment.CurrentManagedThreadId));
            return inner.RedoAsync(cancellationToken);
        }
    }
}
