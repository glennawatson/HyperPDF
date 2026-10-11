// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Text.Fonts;
using PdfViewerLite.Core.Text.Layout;
namespace PdfViewerLite.HyperPdf;

/// <summary>Owns the document's annotation caches and pending edit state.</summary>
[DebuggerDisplay("HyperPdfAnnotations: {_unsavedChanges} unsaved changes")]
internal sealed class HyperPdfAnnotations
{
    /// <summary>The document.</summary>
    private readonly PdfDocument _document;

    /// <summary>The document's objects.</summary>
    private readonly PdfObjectStore _store;

    /// <summary>The names the editor uses that the library does not know.</summary>
    private readonly AnnotationNames _names;

    /// <summary>The clock chosen for this document's annotation dates.</summary>
    private readonly TimeProvider _clock;

    /// <summary>The annotation font state chosen by the engine.</summary>
    private readonly TextBoxFontState _fontState;

    /// <summary>The number of edits since opening or the last save.</summary>
    private int _unsavedChanges;

    /// <summary>The author recorded on new annotations.</summary>
    private string _author = Environment.UserName;

    /// <summary>The installed fonts, or <see langword="null"/> for the system's.</summary>
    private FontCatalog? _catalog;

    /// <summary>Initializes a new instance of the <see cref="HyperPdfAnnotations"/> class.</summary>
    /// <param name="document">The document.</param>
    /// <param name="clock">The clock chosen for this document.</param>
    /// <param name="fontState">The annotation font cache chosen by the engine.</param>
    internal HyperPdfAnnotations(PdfDocument document, TimeProvider clock, TextBoxFontState fontState)
    {
        ArgumentNullException.ThrowIfNull(clock);
        ArgumentNullException.ThrowIfNull(fontState);
        _document = document;
        _store = document.Objects;
        _names = AnnotationNames.Create(_store.Names);
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
    internal AnnotationNames Names => _names;

    /// <summary>Gets the Cache state.</summary>
    internal Dictionary<int, PageAnnotation[]> Cache { get; } = [];

    /// <summary>Gets the PagesWithRemoved state.</summary>
    internal HashSet<int> PagesWithRemoved { get; } = [];

    /// <summary>Gets the Layout state.</summary>
    internal TextBoxLayout Layout { get; } = new();

    /// <summary>Gets the font cache selected by the engine.</summary>
    internal TextBoxFontState FontState => _fontState;

    /// <summary>Gets a reference to the UnsavedChanges state.</summary>
    internal ref int UnsavedChanges => ref _unsavedChanges;

    /// <summary>Gets a reference to the Author state.</summary>
    internal ref string Author => ref _author;

    /// <summary>Gets a reference to the Catalog state.</summary>
    internal ref FontCatalog? Catalog => ref _catalog;
}
