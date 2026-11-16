// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

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
        public IObservable<RxVoid> Apply(PageEditRequest request) => Record(nameof(Apply), () => inner.Apply(request));

        /// <inheritdoc/>
        public IObservable<RxVoid> Apply(PageEditRequest request, PageEditReport report) => Record(nameof(Apply), () => inner.Apply(request, report));

        /// <inheritdoc/>
        public IObservable<RxVoid> Insert(int index, string[] files) => Record(nameof(Insert), () => inner.Insert(index, files));

        /// <inheritdoc/>
        public IObservable<RxVoid> Insert(int index, string[] files, PageEditReport report) => Record(nameof(Insert), () => inner.Insert(index, files, report));

        /// <inheritdoc/>
        public IObservable<RxVoid> Extract(ReadOnlyMemory<int> pages, Stream destination) => Record(nameof(Extract), () => inner.Extract(pages, destination));

        /// <inheritdoc/>
        public IObservable<RxVoid> Extract(ReadOnlyMemory<int> pages, Stream destination, PageEditReport report) => Record(nameof(Extract), () => inner.Extract(pages, destination, report));

        /// <inheritdoc/>
        public IObservable<bool> Undo() => Record(nameof(Undo), inner.Undo);

        /// <inheritdoc/>
        public IObservable<bool> Redo() => Record(nameof(Redo), inner.Redo);

        /// <summary>Records the thread the call is made on, when it is subscribed, then passes it on.</summary>
        /// <typeparam name="T">What the call gives.</typeparam>
        /// <param name="name">The operation.</param>
        /// <param name="call">Starts the real call.</param>
        /// <returns>A cold signal of the real call.</returns>
        private IObservable<T> Record<T>(string name, Func<IObservable<T>> call) =>
            Signal.Defer(() =>
            {
                engine.Calls.Enqueue(new(name, Environment.CurrentManagedThreadId));
                return call();
            });
    }
}
