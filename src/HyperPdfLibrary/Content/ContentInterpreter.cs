// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.
using System.Diagnostics;
using System.Numerics;
using HyperPdfLibrary.Content;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Drawing;
using HyperPdfLibrary.Graphics;
using HyperPdfLibrary.Layers;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Content;

/// <summary>Owns one thread's reusable content interpreter state and drawing resources.</summary>
[DebuggerDisplay("ContentInterpreter: depth {_depth}, stack {_stack.Count}")]
internal sealed class ContentInterpreter : IDisposable
{
    /// <summary>The document's caches.</summary>
    private readonly PdfRenderCache _cache;

    /// <summary>The device everything is drawn on.</summary>
    private readonly IContentDevice _device;

    /// <summary>The device as a text collector told where each text-showing operator starts; null for other devices.</summary>
    private readonly ITextObjectDevice? _textObjects;

    /// <summary>The document's layers.</summary>
    private readonly PdfOptionalContent _layers;

    /// <summary>The current graphics state.</summary>
    private GraphicsState _state = new();

    /// <summary>The current point of the path.</summary>
    private PdfPoint _current;

    /// <summary>The segments added to the current path; zero means the path is empty.</summary>
    private int _pathPoints;

    /// <summary>The glyph outlines added to the text clip since it was last applied.</summary>
    private int _textClipGlyphs;

    /// <summary>The text matrix.</summary>
    private Matrix3x2 _tm = Matrix3x2.Identity;

    /// <summary>The text line matrix.</summary>
    private Matrix3x2 _tlm = Matrix3x2.Identity;

    /// <summary>The matrix from the current stream's default space to the page, which patterns are placed against.</summary>
    private Matrix3x2 _patternBase = Matrix3x2.Identity;

    /// <summary>The nesting of forms, patterns and Type 3 glyphs being run.</summary>
    private int _depth;

    /// <summary>The state stack depth the current stream may not restore below.</summary>
    private int _stackFloor;

    /// <summary>The marked content depth the current stream may not close below.</summary>
    private int _markedFloor;

    /// <summary>The number of open marked content levels that hide their content.</summary>
    private int _hidden;

    /// <summary>Whether the next painting operator applies a clip.</summary>
    private bool _pendingClip;

    /// <summary>Whether the pending clip uses the even-odd rule.</summary>
    private bool _pendingClipEvenOdd;

    /// <summary>Whether colour operators are ignored, as inside uncoloured patterns and Type 3 glyphs that give only a shape.</summary>
    private bool _colorLocked;

    /// <summary>Initializes a new instance of the <see cref = "ContentInterpreter"/> class.</summary>
    /// <param name = "cache">The document's caches.</param>
    /// <param name = "device">The device to draw on.</param>
    /// <param name = "depth">The nesting depth already in use, for streams run from inside others.</param>
    /// <param name="imageScale">The upper device scale for selecting decoded image resolution.</param>
    internal ContentInterpreter(PdfRenderCache cache, IContentDevice device, int depth, float imageScale = float.PositiveInfinity)
    {
        _cache = cache;
        _device = device;
        _textObjects = device as ITextObjectDevice;
        _layers = PdfDocumentLayers.GetOptionalContent(cache.Document);
        _depth = depth;
        ImageScale = imageScale;
    }

    /// <summary>Gets the upper device scale for decoded images in this recording.</summary>
    internal float ImageScale { get; }

    /// <summary>Gets the interpreter's Cache state.</summary>
    internal PdfRenderCache Cache => _cache;

    /// <summary>Gets the interpreter's Device state.</summary>
    internal IContentDevice Device => _device;

    /// <summary>Gets the interpreter's TextObjects state.</summary>
    internal ITextObjectDevice? TextObjects => _textObjects;

    /// <summary>Gets the interpreter's Layers state.</summary>
    internal PdfOptionalContent Layers => _layers;

    /// <summary>Gets the interpreter's Stack state.</summary>
    internal List<GraphicsState> Stack { get; } = [with(ContentExecution.StackCapacity)];

    /// <summary>Gets the interpreter's Resources state.</summary>
    internal List<PdfDictionary?> Resources { get; } = [];

    /// <summary>Gets the interpreter's Marked state.</summary>
    internal List<bool> Marked { get; } = [];

    /// <summary>Gets the interpreter's Path state.</summary>
    internal PdfPathBuilder Path { get; } = new();

    /// <summary>Gets the interpreter's TextClip state.</summary>
    internal PdfPathBuilder TextClip { get; } = new();

    /// <summary>Gets the current graphics state by reference.</summary>
    internal ref GraphicsState State => ref _state;

    /// <summary>Gets or sets the interpreter's Current state.</summary>
    internal ref PdfPoint Current => ref _current;

    /// <summary>Gets or sets the interpreter's PathPoints state.</summary>
    internal ref int PathPoints => ref _pathPoints;

    /// <summary>Gets or sets the interpreter's TextClipGlyphs state.</summary>
    internal ref int TextClipGlyphs => ref _textClipGlyphs;

    /// <summary>Gets or sets the interpreter's Tm state.</summary>
    internal ref Matrix3x2 Tm => ref _tm;

    /// <summary>Gets or sets the interpreter's Tlm state.</summary>
    internal ref Matrix3x2 Tlm => ref _tlm;

    /// <summary>Gets or sets the interpreter's PatternBase state.</summary>
    internal ref Matrix3x2 PatternBase => ref _patternBase;

    /// <summary>Gets or sets the interpreter's Depth state.</summary>
    internal ref int Depth => ref _depth;

    /// <summary>Gets or sets the interpreter's StackFloor state.</summary>
    internal ref int StackFloor => ref _stackFloor;

    /// <summary>Gets or sets the interpreter's MarkedFloor state.</summary>
    internal ref int MarkedFloor => ref _markedFloor;

    /// <summary>Gets or sets the interpreter's Hidden state.</summary>
    internal ref int Hidden => ref _hidden;

    /// <summary>Gets or sets the interpreter's PendingClip state.</summary>
    internal ref bool PendingClip => ref _pendingClip;

    /// <summary>Gets or sets the interpreter's PendingClipEvenOdd state.</summary>
    internal ref bool PendingClipEvenOdd => ref _pendingClipEvenOdd;

    /// <summary>Gets or sets the interpreter's ColorLocked state.</summary>
    internal ref bool ColorLocked => ref _colorLocked;

    /// <summary>Gets or sets a value indicating whether optional content follows print usage rather than the viewer's layer switches.</summary>
    internal bool Printing { get; set; }

    /// <inheritdoc/>
    public void Dispose()
    {
    }
}
