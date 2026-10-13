// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>
/// A font being embedded in a document as a composite (Type 0) font. Text shown through it is written as two-byte glyph
/// codes, and the glyphs used are recorded. <see cref="Finish"/> then cuts the font program down to those glyphs and
/// writes the font's objects, so the object <see cref="Resource"/> points to is complete. Create one with
/// <see cref="Create(PdfObjectStore, PdfFontSource)"/> and put <see cref="Resource"/> in a resource dictionary's
/// <c>/Font</c> entry. Members are safe to call from several threads.
/// </summary>
[DebuggerDisplay("PdfEmbeddedFont: {Source.PostScriptName}, {_use.Count} glyphs, finished {_finished}")]
public sealed class PdfEmbeddedFont
{
    /// <summary>The fonts embedded in each document that are not finished yet.</summary>
    private static readonly ConditionalWeakTable<object, List<PdfEmbeddedFont>> Pending = [];

    /// <summary>The document.</summary>
    private readonly IPdfObjectSink _sink;

    /// <summary>The options.</summary>
    private readonly PdfEmbeddedFontOptions _options;

    /// <summary>The glyphs used.</summary>
    private readonly EmbeddedGlyphUse _use;

    /// <summary>Shapes text that is shown without glyphs.</summary>
    private readonly IPdfGlyphShaper _shaper;

    /// <summary>What the shaper is asked to do with each run.</summary>
    private readonly PdfShapingOptions _shapingOptions;

    /// <summary>Guards finishing.</summary>
    private readonly Lock _finishGate = new();

    /// <summary>Whether the font's objects are complete.</summary>
    private volatile bool _finished;

    /// <summary>Initializes a new instance of the <see cref="PdfEmbeddedFont"/> class.</summary>
    /// <param name="sink">The document.</param>
    /// <param name="source">The font.</param>
    /// <param name="options">The options.</param>
    /// <param name="id">The id of the font dictionary, which is replaced when the font is finished.</param>
    internal PdfEmbeddedFont(IPdfObjectSink sink, PdfFontSource source, PdfEmbeddedFontOptions options, PdfObjectId id)
    {
        _sink = sink;
        Source = source;
        _options = options;
        Id = id;
        _use = new(source.GlyphCount, source.CanSubset ? null : source.GetWholeFontCids());
        _shaper = options.Shaper ?? source.DefaultShaper;
        _shapingOptions = new(options.WritingMode) { Language = options.Language, Script = options.Script, Direction = options.Direction, Features = options.Features };
    }

    /// <summary>Gets the font file this font was made from.</summary>
    public PdfFontSource Source { get; }

    /// <summary>Gets a value indicating whether <see cref="Finish"/> has written the font's objects.</summary>
    public bool IsFinished => _finished;

    /// <summary>Gets the id of the font dictionary.</summary>
    public PdfObjectId Id { get; }

    /// <summary>Gets a reference to the font dictionary, for a resource dictionary's <c>/Font</c> entry.</summary>
    public PdfValue Resource => PdfValue.FromReference(Id);

    /// <summary>Starts embedding a font in an open document with the default options.</summary>
    /// <param name="store">The open document.</param>
    /// <param name="source">The font, opened with <see cref="PdfFontSource.Open(byte[], int)"/>.</param>
    /// <returns>The font; show text through it, then call <see cref="Finish"/> or <see cref="FinishAll(PdfObjectStore)"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfEmbeddedFont Create(PdfObjectStore store, PdfFontSource source) => Create(store, source, PdfEmbeddedFontOptions.Default);

    /// <summary>Starts embedding a font in an open document.</summary>
    /// <param name="store">The open document.</param>
    /// <param name="source">The font, opened with <see cref="PdfFontSource.Open(byte[], int)"/>.</param>
    /// <param name="options">How the font is embedded and how text is shown with it.</param>
    /// <returns>The font; show text through it, then call <see cref="Finish"/> or <see cref="FinishAll(PdfObjectStore)"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static PdfEmbeddedFont Create(PdfObjectStore store, PdfFontSource source, PdfEmbeddedFontOptions options)
    {
        ArgumentNullException.ThrowIfNull(store);
        return Create(new PdfObjectStoreSink(store), source, options);
    }

    /// <summary>Starts embedding a font in a document being built or edited, with the default options.</summary>
    /// <param name="sink">Where the font's objects go.</param>
    /// <param name="source">The font, opened with <see cref="PdfFontSource.Open(byte[], int)"/>.</param>
    /// <returns>The font; show text through it, then call <see cref="Finish"/> or <see cref="FinishAll(IPdfObjectSink)"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfEmbeddedFont Create(IPdfObjectSink sink, PdfFontSource source) => Create(sink, source, PdfEmbeddedFontOptions.Default);

    /// <summary>Starts embedding a font in a document being built or edited.</summary>
    /// <param name="sink">Where the font's objects go.</param>
    /// <param name="source">The font, opened with <see cref="PdfFontSource.Open(byte[], int)"/>.</param>
    /// <param name="options">How the font is embedded and how text is shown with it.</param>
    /// <returns>The font; show text through it, then call <see cref="Finish"/> or <see cref="FinishAll(IPdfObjectSink)"/>.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    public static PdfEmbeddedFont Create(IPdfObjectSink sink, PdfFontSource source, PdfEmbeddedFontOptions options)
    {
        ArgumentNullException.ThrowIfNull(sink);
        ArgumentNullException.ThrowIfNull(source);
        ArgumentNullException.ThrowIfNull(options);
        var id = sink.Add(PdfValue.FromDictionary(EmbeddedFontWriter.CreatePlaceholder(sink, source.PostScriptName, options.WritingMode)));
        var font = new PdfEmbeddedFont(sink, source, options, id);
        var fonts = Pending.GetOrCreateValue(KeyOf(sink));
        lock (fonts)
        {
            fonts.Add(font);
        }

        return font;
    }

    /// <summary>Finishes every font embedded in an open document that is not finished yet; call it before saving.</summary>
    /// <param name="store">The open document.</param>
    /// <exception cref="ArgumentNullException"><paramref name="store"/> is <see langword="null"/>.</exception>
    public static void FinishAll(PdfObjectStore store)
    {
        ArgumentNullException.ThrowIfNull(store);
        FinishAll(new PdfObjectStoreSink(store));
    }

    /// <summary>Finishes every font embedded in a document that is not finished yet; call it before saving.</summary>
    /// <param name="sink">The document the fonts were created in.</param>
    /// <exception cref="ArgumentNullException"><paramref name="sink"/> is <see langword="null"/>.</exception>
    public static void FinishAll(IPdfObjectSink sink)
    {
        ArgumentNullException.ThrowIfNull(sink);
        if (!Pending.TryGetValue(KeyOf(sink), out var fonts))
        {
            return;
        }

        PdfEmbeddedFont[] snapshot;
        lock (fonts)
        {
            snapshot = [.. fonts];
        }

        // A font leaves the list only once it is finished, so a font that fails stays for the next call, and a second caller
        // waits in Finish for the first caller's font rather than returning before it is written.
        foreach (var font in snapshot)
        {
            font.Finish();
            lock (fonts)
            {
                _ = fonts.Remove(font);
            }
        }
    }

    /// <summary>Gets the code that shows a glyph, giving the glyph a code the first time it is used.</summary>
    /// <param name="glyph">The glyph in the font file.</param>
    /// <returns>The two-byte code; zero (the missing glyph) when the glyph is not in the font.</returns>
    /// <exception cref="InvalidOperationException">The font is finished and the glyph was not used before.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public int GetCode(int glyph) => _use.GetCode(glyph);

    /// <summary>Shapes text with the font's shaper.</summary>
    /// <param name="text">The text in logical order.</param>
    /// <param name="output">Receives the glyphs in drawing order. The list is not cleared.</param>
    /// <returns><see langword="true"/> when the text reads right to left.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="output"/> is <see langword="null"/>.</exception>
    public bool Shape(ReadOnlySpan<char> text, List<PdfShapedGlyph> output)
    {
        ArgumentNullException.ThrowIfNull(output);
        return _shaper.Shape(text, _shapingOptions, output);
    }

    /// <summary>Measures how far the text advances.</summary>
    /// <param name="text">The text.</param>
    /// <param name="fontSize">The font size.</param>
    /// <returns>The advance, in the units of <paramref name="fontSize"/>.</returns>
    public float Measure(ReadOnlySpan<char> text, float fontSize)
    {
        var glyphs = new List<PdfShapedGlyph>(text.Length);
        _ = Shape(text, glyphs);
        var advance = 0F;
        foreach (var glyph in glyphs)
        {
            advance += glyph.Advance;
        }

        return advance * fontSize;
    }

    /// <summary>Records the text a run of shaped glyphs stands for, without writing content; used when the caller writes the codes itself.</summary>
    /// <param name="text">The text in logical order.</param>
    /// <param name="glyphs">The glyphs in drawing order.</param>
    /// <exception cref="InvalidOperationException">The font is finished and a glyph was not used before.</exception>
    public void Register(ReadOnlySpan<char> text, ReadOnlySpan<PdfShapedGlyph> glyphs)
    {
        var groups = new List<ClusterGroup>();
        RunPlanner.Plan(text, glyphs, _options.ActualText, _use, groups);
    }

    /// <summary>Shapes text and writes it into a content stream with the font's current text state.</summary>
    /// <param name="builder">The content stream, inside a text object with this font selected.</param>
    /// <param name="text">The text in logical order, without line breaks.</param>
    /// <param name="fontSize">The font size selected with <c>Tf</c>.</param>
    /// <exception cref="InvalidOperationException">The font is finished and the text needs a glyph that was not used before.</exception>
    public void ShowText(ref PdfContentBuilder builder, ReadOnlySpan<char> text, float fontSize)
    {
        var glyphs = new List<PdfShapedGlyph>(text.Length);
        _ = Shape(text, glyphs);
        ShowGlyphs(ref builder, text, CollectionsMarshal.AsSpan(glyphs), fontSize);
    }

    /// <summary>Writes shaped glyphs into a content stream with the font's current text state.</summary>
    /// <param name="builder">The content stream, inside a text object with this font selected.</param>
    /// <param name="text">The text the glyphs were shaped from, in logical order.</param>
    /// <param name="glyphs">The glyphs in drawing order.</param>
    /// <param name="fontSize">The font size selected with <c>Tf</c>.</param>
    /// <exception cref="InvalidOperationException">The font is finished and a glyph was not used before.</exception>
    public void ShowGlyphs(ref PdfContentBuilder builder, ReadOnlySpan<char> text, ReadOnlySpan<PdfShapedGlyph> glyphs, float fontSize)
    {
        var groups = new List<ClusterGroup>();
        RunPlanner.Plan(text, glyphs, _options.ActualText, _use, groups);
        RunWriter.Write(ref builder, new(_sink, Source, _use, _options.WritingMode), text, glyphs, groups, fontSize);
    }

    /// <summary>Cuts the font program down to the glyphs used and writes the font's objects. Calling it again does nothing.</summary>
    public void Finish()
    {
        lock (_finishGate)
        {
            if (_finished)
            {
                return;
            }

            var parts = Plan(_use.Freeze());
            _sink.Set(Id, PdfValue.FromDictionary(EmbeddedFontWriter.Write(_sink, parts)));
            _finished = true;
        }
    }

    /// <summary>Gets the object that identifies a document, so fonts made through different sinks of one open document are finished together.</summary>
    /// <param name="sink">The sink.</param>
    /// <returns>The open document for a sink onto one; the sink otherwise.</returns>
    private static object KeyOf(IPdfObjectSink sink) => sink.Owner is { } owner ? owner : sink;

    /// <summary>Plans the font's objects from the glyphs used.</summary>
    /// <param name="snapshot">The glyphs used.</param>
    /// <returns>The font's parts.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private EmbeddedFontParts Plan(GlyphUseSnapshot snapshot)
    {
        var parts = Source.Outlines == PdfFontOutlines.TrueType
            ? EmbeddedFontPlanner.PlanTrueType(Source, snapshot, _options.WriteCidSet)
            : EmbeddedFontPlanner.PlanCff(Source, snapshot, _options.WriteCidSet);
        return _options.WritingMode == PdfWritingMode.Vertical
            ? parts with { Vertical = VerticalMetrics.Create(Source, EmbeddedFontPlanner.With(0, snapshot.Glyphs)) }
            : parts;
    }
}
