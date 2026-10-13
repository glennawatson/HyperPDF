// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Reading;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// The document's pages in reading order for one tab, shared by Read Aloud, Focus Mode and the screen reader view. It
/// is worked out again when the document is reloaded, its pages change or its page text changes, and it says when that
/// happens so a view of the text can load it again.
/// </summary>
[DebuggerDisplay("TabReading: Version {Version}")]
internal sealed class TabReading : IDisposable
{
    /// <summary>The tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Emits when the text a reader sees may have changed.</summary>
    private readonly Signal<RxVoid> _changes = new();

    /// <summary>The document's pages in reading order, once asked for.</summary>
    private ReadingDocument? _reading;

    /// <summary>The source identity the reading order was worked out for.</summary>
    private int _readingFor = -1;

    /// <summary>The open document the reading order reads from, captured on the UI thread.</summary>
    private IDocument? _readingSource;

    /// <summary>How many times the text or pages were reset; it only grows.</summary>
    private int _generation;

    /// <summary>Initializes a new instance of the <see cref="TabReading"/> class.</summary>
    /// <param name="owner">The tab.</param>
    internal TabReading(DocumentTabViewModel owner) => _owner = owner;

    /// <summary>Gets notifications that the reading text changed: after a reload, a change of pages or recognised text.</summary>
    internal IObservable<RxVoid> Changes => _changes;

    /// <summary>Gets a number that differs whenever the text a reader sees may differ: the document the tab shows and how often its text was reset.</summary>
    internal long Version => ((long)_owner.Source.Id << 32) | (uint)Volatile.Read(ref _generation);

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _changes.Dispose();

    /// <summary>Gets the document's pages in reading order.</summary>
    /// <returns>The reading order, or <see langword="null"/> when the document is closed or cannot describe its layout.</returns>
    internal ReadingDocument? Get()
    {
        var document = _owner.TryGetDocument();
        if (document is null || DocumentFeature<ITextLayoutSource>.Get(document) is null)
        {
            return null;
        }

        // Worker threads use this captured document, never the UI-owned document pool.
        Volatile.Write(ref _readingSource, document);
        if (_reading is null || _readingFor != _owner.Source.Id)
        {
            _reading = ReadingDocument.Create(CurrentSources, _owner.Source.PageSizes);
            _readingFor = _owner.Source.Id;
        }

        return _reading;
    }

    /// <summary>Forgets the worked out text after the page text changed, for example once text is recognised.</summary>
    internal void Invalidate()
    {
        _reading?.Clear();
        Publish();
    }

    /// <summary>Forgets the reading order altogether, because the pages changed.</summary>
    internal void Reset()
    {
        _reading = null;
        _readingFor = -1;
        Publish();
    }

    /// <summary>Gets layout and optional tagged structure from the captured document, from any thread.</summary>
    /// <returns>Both reading capabilities, or <see langword="null"/> when the document is closed or has no layout.</returns>
    private ReadingSources? CurrentSources()
    {
        var document = Volatile.Read(ref _readingSource);
        return document is null || document.IsDisposed
            || DocumentFeature<ITextLayoutSource>.Get(document) is not { } characters
            ? null
            : new ReadingSources(characters, DocumentFeature<ITaggedStructureSource>.Get(document));
    }

    /// <summary>Moves the version on and tells readers of the text.</summary>
    private void Publish()
    {
        _ = Interlocked.Increment(ref _generation);
        _changes.EmitIfOpen(RxVoid.Default);
    }
}
