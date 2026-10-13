// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Text.Fonts;
using HyperPdf.Core.Text.Layout;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
namespace HyperPdf.Engine;

/// <summary>
/// Owns the document's annotation caches and pending edit state. The caches, the name index and the text layout are made
/// when an edit first needs them, so writing a text layer onto a scanned page makes none of them.
/// </summary>
[DebuggerDisplay("HyperPdfAnnotations: {_unsavedChanges} unsaved changes")]
internal sealed class HyperPdfAnnotations
{
    /// <summary>The document.</summary>
    private readonly PdfDocument _document;

    /// <summary>The document's objects.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>The clock chosen for this document's annotation dates.</summary>
    private readonly TimeProvider _clock;

    /// <summary>The annotation font state chosen by the engine, built when a font is first needed.</summary>
    private readonly Lazy<TextBoxFontState> _fontState;

    /// <summary>The names the editor uses that the library does not know; made on first use.</summary>
    private AnnotationNames? _names;

    /// <summary>The annotations read from each page; made on first use.</summary>
    private Dictionary<int, PageAnnotation[]>? _cache;

    /// <summary>The pages with removed annotations; made when one is removed.</summary>
    private HashSet<int>? _pagesWithRemoved;

    /// <summary>The text box layout; made on first use.</summary>
    private TextBoxLayout? _layout;

    /// <summary>The number of edits since opening or the last save.</summary>
    private int _unsavedChanges;

    /// <summary>The author recorded on new annotations.</summary>
    private string _author = Environment.UserName;

    /// <summary>The installed fonts, or <see langword="null"/> for the system's.</summary>
    private FontCatalog? _catalog;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfAnnotations"/> class.</summary>
    /// <param name="document">The document.</param>
    /// <param name="clock">The clock chosen for this document.</param>
    /// <param name="fontState">The annotation font cache chosen by the engine, built on first use.</param>
    internal HyperPdfAnnotations(PdfDocument document, TimeProvider clock, Lazy<TextBoxFontState> fontState)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(fontState);
        _document = document;
        _store = document.Objects;
        _clock = clock;
        _fontState = fontState;
    }

    /// <summary>Gets the clock used for this document's annotation modification dates.</summary>
    internal TimeProvider Clock => _clock;

    /// <summary>Gets the Gate state.</summary>
    internal Lock Gate { get; } = new();

    /// <summary>Gets the Document state.</summary>
    internal PdfDocument Document => _document;

    /// <summary>Gets the Store state.</summary>
    internal PdfObjectStore Store => _store;

    /// <summary>Gets the Names state.</summary>
    internal AnnotationNames Names => Volatile.Read(ref _names) ?? Interlocked.CompareExchange(ref _names, AnnotationNames.Create(_store.Names), null) ?? Volatile.Read(ref _names)!;

    /// <summary>Gets the Cache state.</summary>
    internal Dictionary<int, PageAnnotation[]> Cache => Volatile.Read(ref _cache) ?? Interlocked.CompareExchange(ref _cache, [], null) ?? Volatile.Read(ref _cache)!;

    /// <summary>Gets the PagesWithRemoved state.</summary>
    internal HashSet<int> PagesWithRemoved => Volatile.Read(ref _pagesWithRemoved) ?? Interlocked.CompareExchange(ref _pagesWithRemoved, [], null) ?? Volatile.Read(ref _pagesWithRemoved)!;

    /// <summary>Gets the number of pages with removed annotations, without making the set when none were removed.</summary>
    internal int RemovedPageCount => Volatile.Read(ref _pagesWithRemoved)?.Count ?? 0;

    /// <summary>Gets the Layout state.</summary>
    internal TextBoxLayout Layout => Volatile.Read(ref _layout) ?? Interlocked.CompareExchange(ref _layout, new(), null) ?? Volatile.Read(ref _layout)!;

    /// <summary>Gets the font cache selected by the engine.</summary>
    internal TextBoxFontState FontState => _fontState.Value;

    /// <summary>Gets the font cache selected by the engine, which is built when a caller first reads its value.</summary>
    internal Lazy<TextBoxFontState> LazyFontState => _fontState;

    /// <summary>Gets a reference to the UnsavedChanges state.</summary>
    internal ref int UnsavedChanges => ref _unsavedChanges;

    /// <summary>Gets a reference to the Author state.</summary>
    internal ref string Author => ref _author;

    /// <summary>Gets a reference to the Catalog state.</summary>
    internal ref FontCatalog? Catalog => ref _catalog;

    /// <summary>Forgets the cached annotations of a page, if any were cached.</summary>
    /// <param name="pageIndex">The page.</param>
    internal void ForgetPage(int pageIndex) => _ = Volatile.Read(ref _cache)?.Remove(pageIndex);

    /// <summary>Forgets every cached page and every page with removed annotations, if any were made.</summary>
    internal void ForgetPages()
    {
        Volatile.Read(ref _cache)?.Clear();
        Volatile.Read(ref _pagesWithRemoved)?.Clear();
    }
}
