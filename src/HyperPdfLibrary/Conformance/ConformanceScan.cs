// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Fonts;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Counts the features a conformance reading report lists. Filters, scripts and external content are read from every
/// object. Fonts, transparency and device colour are read only from what page content and annotation appearances use,
/// as ISO 19005-1 Cor.2 §6.2.10 exempts named resources the content does not use.
/// </summary>
[DebuggerDisplay("ConformanceScan: {_lzw} lzw, {_scripts} scripts, {_fonts.Count} fonts")]
internal sealed class ConformanceScan
{
    /// <summary>The most font names listed in an observation's detail.</summary>
    private const int MaxListedFonts = 8;

    /// <summary>The name listed for a font without a <c>/BaseFont</c>.</summary>
    private const string UnnamedFont = "unnamed font";

    /// <summary>The document.</summary>
    private readonly PdfDocument _document;

    /// <summary>The names of fonts without an embedded program.</summary>
    private readonly List<string> _fonts = [];

    /// <summary>The interned <c>/ca</c> name, which the known-name list does not hold.</summary>
    private readonly PdfName _lowerCa;

    /// <summary>The streams that use LZW.</summary>
    private int _lzw;

    /// <summary>The JavaScript actions and scripts.</summary>
    private int _scripts;

    /// <summary>The soft masks.</summary>
    private int _softMasks;

    /// <summary>The constant alpha values below 1.</summary>
    private int _alphas;

    /// <summary>The blend modes other than Normal.</summary>
    private int _blendModes;

    /// <summary>The transparency groups.</summary>
    private int _groups;

    /// <summary>The streams that refer to external content.</summary>
    private int _external;

    /// <summary>Whether drawn content uses device colour.</summary>
    private bool _usesDeviceColor;

    /// <summary>Initializes a new instance of the <see cref="ConformanceScan"/> class.</summary>
    /// <param name="document">The document.</param>
    internal ConformanceScan(PdfDocument document)
    {
        _document = document;
        _lowerCa = document.Objects.Names.Intern("ca");
    }

    /// <summary>Runs the scan and lists what it saw.</summary>
    /// <param name="claim">The PDF/A claim, or null.</param>
    /// <param name="hasOutputIntent">Whether the catalog has an output intent.</param>
    /// <param name="cancellationToken">Stops the scan between objects and pages.</param>
    /// <returns>The observations in finding order.</returns>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal PdfConformanceObservation[] Run(PdfAClaim? claim, bool hasOutputIntent, CancellationToken cancellationToken)
    {
        ScanObjects(cancellationToken);
        var uses = ConformanceResources.Read(_document, cancellationToken);
        ScanFonts(uses);
        ScanTransparency(uses);
        _usesDeviceColor = !hasOutputIntent && UsesDeviceColor(uses);

        var observations = new List<PdfConformanceObservation>();
        AddIf(observations, PdfConformanceFinding.Encryption, _document.IsEncrypted ? 1 : 0, null, claim is not null);
        AddIf(observations, PdfConformanceFinding.JavaScript, _scripts, null, claim is not null);
        AddIf(observations, PdfConformanceFinding.LzwFilter, _lzw, null, claim is { ForbidsLzw: true });
        AddIf(observations, PdfConformanceFinding.NonEmbeddedFont, _fonts.Count, FontDetail(), claim is not null);
        var transparent = _softMasks + _alphas + _blendModes + _groups;
        AddIf(observations, PdfConformanceFinding.Transparency, transparent, TransparencyDetail(), claim is { ForbidsTransparency: true });
        var files = CountEmbeddedFiles();
        AddIf(observations, PdfConformanceFinding.EmbeddedFiles, files, null, claim is { ForbidsEmbeddedFiles: true });
        AddIf(observations, PdfConformanceFinding.ExternalContent, _external, null, claim is not null);
        AddIf(observations, PdfConformanceFinding.MissingOutputIntent, _usesDeviceColor ? 1 : 0, null, claim is not null);
        return [.. observations];
    }

    /// <summary>Adds an observation when the count is above zero.</summary>
    /// <param name="observations">The list to add to.</param>
    /// <param name="kind">The finding.</param>
    /// <param name="count">How many were seen.</param>
    /// <param name="detail">The detail text, or null.</param>
    /// <param name="forbidden">Whether the claimed part forbids the feature.</param>
    private static void AddIf(List<PdfConformanceObservation> observations, PdfConformanceFinding kind, int count, string? detail, bool forbidden)
    {
        if (count > 0)
        {
            observations.Add(new(kind, count, detail, forbidden));
        }
    }

    /// <summary>Determines whether a value is a number below 1.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsBelowOne(PdfValue value) => value.IsNumber && value.AsNumber() < 1;

    /// <summary>Determines whether a <c>/BM</c> value names a blend mode other than Normal; an array is read by its first name.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when the mode changes how the page composites.</returns>
    private static bool IsBlended(PdfValue value)
    {
        if (value.AsArray() is { } modes)
        {
            value = modes.Get(0);
        }

        return value.AsName() is { IsNone: false } name && name.ToKnownName() is not (KnownName.Normal or KnownName.Compatible);
    }

    /// <summary>Determines whether a value names a soft mask, as opposed to being absent or <c>/None</c>.</summary>
    /// <param name="value">The raw <c>/SMask</c> value.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool IsSoftMask(PdfValue value) => !value.IsNull && !value.IsName(KnownName.NoneName);

    /// <summary>Determines whether a dictionary has a transparency group.</summary>
    /// <param name="dictionary">A page, form or appearance dictionary.</param>
    /// <returns><see langword="true"/> when its <c>/Group</c> is a transparency group.</returns>
    private static bool HasTransparencyGroup(PdfDictionary dictionary) =>
        dictionary.GetDictionary(KnownName.Group) is { } group && group.IsName(KnownName.S, KnownName.Transparency);

    /// <summary>Determines whether a <c>/Filter</c> value includes LZW.</summary>
    /// <param name="filter">The value: a name or an array of names.</param>
    /// <returns><see langword="true"/> when LZW is in the chain.</returns>
    private static bool UsesLzw(PdfValue filter)
    {
        if (filter.AsArray() is not { } chain)
        {
            return IsLzw(filter);
        }

        for (var i = 0; i < chain.Count; i++)
        {
            if (IsLzw(chain.Get(i)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Determines whether a value is the LZW filter name.</summary>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when it is.</returns>
    private static bool IsLzw(PdfValue value) => value.IsName(KnownName.LZWDecode) || value.IsName(KnownName.LZW);

    /// <summary>Determines whether a font should carry an embedded program but does not. Type 3 fonts draw their own glyphs and are skipped.</summary>
    /// <param name="font">The font dictionary.</param>
    /// <returns><see langword="true"/> when the font has no <c>/FontFile</c>, <c>/FontFile2</c> or <c>/FontFile3</c>.</returns>
    private static bool IsNotEmbedded(PdfDictionary font)
    {
        if (font.IsName(KnownName.Subtype, KnownName.Type3))
        {
            return false;
        }

        var descriptorOwner = font.IsName(KnownName.Subtype, KnownName.Type0) ? font.GetArray(KnownName.DescendantFonts)?.GetDictionary(0) : font;
        return !FontDescriptor.Read(descriptorOwner?.GetDictionary(KnownName.FontDescriptor)).IsEmbedded;
    }

    /// <summary>Determines whether a colour space value names a device space.</summary>
    /// <param name="space">The value.</param>
    /// <returns><see langword="true"/> for DeviceGray, DeviceRGB and DeviceCMYK.</returns>
    private static bool IsDeviceSpace(PdfValue space) => space.IsName(KnownName.DeviceGray) || space.IsName(KnownName.DeviceRGB) || space.IsName(KnownName.DeviceCMYK);

    /// <summary>
    /// Determines whether drawn content uses device colour: a colour operator or device colour space in page, form,
    /// pattern or appearance content, or an image drawn in a device colour space.
    /// </summary>
    /// <param name="uses">The used resources.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool UsesDeviceColor(ConformanceUses uses)
    {
        if (uses.ColorOperators)
        {
            return true;
        }

        foreach (var image in uses.Images)
        {
            if (IsDeviceSpace(image.Get(KnownName.ColorSpace)))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Counts the embedded files: the name tree entries, or the <c>/AF</c> entries when there are more.</summary>
    /// <returns>The count.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private int CountEmbeddedFiles() =>
        Math.Max(PdfDocumentAttachments.GetAttachments(_document).Count, _document.Catalog.GetArray(_document.Objects.Names.Intern("AF"))?.Count ?? 0);

    /// <summary>Joins the listed font names.</summary>
    /// <returns>The names, with a count of the rest when there are more than the limit.</returns>
    private string? FontDetail()
    {
        if (_fonts.Count == 0)
        {
            return null;
        }

        var listed = string.Join(", ", _fonts.GetRange(0, Math.Min(_fonts.Count, MaxListedFonts)));
        return _fonts.Count > MaxListedFonts ? string.Create(CultureInfo.InvariantCulture, $"{listed} and {_fonts.Count - MaxListedFonts} more") : listed;
    }

    /// <summary>Describes which transparency features were seen.</summary>
    /// <returns>The text, or null when none were.</returns>
    private string? TransparencyDetail() =>
        _softMasks + _alphas + _blendModes + _groups == 0
            ? null
            : string.Create(CultureInfo.InvariantCulture, $"soft masks {_softMasks}, alpha below 1 {_alphas}, blend modes {_blendModes}, groups {_groups}");

    /// <summary>Reads every object once and counts LZW filters, scripts and external content.</summary>
    /// <param name="cancellationToken">Stops the scan between objects.</param>
    private void ScanObjects(CancellationToken cancellationToken)
    {
        var objects = _document.Objects;
        for (var number = 1; number < objects.Size; number++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            PdfOpenContext.ThrowIfCancelled(objects.Context);
            var value = StoreReading.GetObject(objects, new(number, 0));
            if (value.AsStream() is { } stream)
            {
                CheckStream(stream.Dictionary);
            }

            ScanValue(value, 0);
        }
    }

    /// <summary>Counts the stream-only features of a stream dictionary: LZW and external content.</summary>
    /// <param name="dictionary">The stream's dictionary.</param>
    private void CheckStream(PdfDictionary dictionary)
    {
        if (UsesLzw(dictionary.Get(KnownName.Filter)))
        {
            _lzw++;
        }

        if (dictionary.ContainsKey(KnownName.F) || dictionary.ContainsKey(KnownName.Ref) || dictionary.ContainsKey(KnownName.OPI))
        {
            _external++;
        }
    }

    /// <summary>Walks the direct dictionaries and arrays inside a value for scripts. References are not followed; each object is scanned on its own.</summary>
    /// <param name="value">The value.</param>
    /// <param name="depth">The nesting depth.</param>
    private void ScanValue(PdfValue value, int depth)
    {
        if (depth >= PdfLimits.MaxNesting)
        {
            return;
        }

        if (value.AsDictionary() is { } dictionary)
        {
            if (dictionary.IsName(KnownName.S, KnownName.JavaScript))
            {
                _scripts++;
            }

            for (var i = 0; i < dictionary.Count; i++)
            {
                ScanValue(dictionary.GetValueAt(i), depth + 1);
            }
        }
        else if (value.AsArray() is { } array)
        {
            for (var i = 0; i < array.Count; i++)
            {
                ScanValue(array.GetRaw(i), depth + 1);
            }
        }
    }

    /// <summary>Records the used fonts that have no embedded program.</summary>
    /// <param name="uses">The used resources.</param>
    private void ScanFonts(ConformanceUses uses)
    {
        foreach (var font in uses.Fonts)
        {
            if (IsNotEmbedded(font))
            {
                _fonts.Add(_document.Objects.Names.GetString(font.GetName(KnownName.BaseFont)) is { Length: > 0 } baseFont ? baseFont : UnnamedFont);
            }
        }
    }

    /// <summary>Counts the transparency that drawn content uses: graphics states, image soft masks, groups and annotation opacity.</summary>
    /// <param name="uses">The used resources.</param>
    private void ScanTransparency(ConformanceUses uses)
    {
        ScanGraphicsStates(uses.GraphicsStates);
        foreach (var image in uses.Images)
        {
            _softMasks += IsSoftMask(image.GetRaw(KnownName.SMask)) ? 1 : 0;
        }

        foreach (var form in uses.Forms)
        {
            _groups += HasTransparencyGroup(form.Dictionary) ? 1 : 0;
        }

        for (var i = 0; i < _document.PageCount; i++)
        {
            var page = PdfDocumentPages.GetPage(_document, i);
            _groups += HasTransparencyGroup(page.Dictionary) ? 1 : 0;
            ScanAnnotationTransparency(page);
        }
    }

    /// <summary>Counts the soft masks, alpha below 1 and blend modes of the graphics states content sets.</summary>
    /// <param name="states">The used graphics state dictionaries.</param>
    private void ScanGraphicsStates(List<PdfDictionary> states)
    {
        foreach (var state in states)
        {
            _softMasks += IsSoftMask(state.GetRaw(KnownName.SMask)) ? 1 : 0;
            _alphas += IsBelowOne(state.Get(KnownName.CA)) || IsBelowOne(state.Get(_lowerCa)) ? 1 : 0;
            _blendModes += IsBlended(state.Get(KnownName.BM)) ? 1 : 0;
        }
    }

    /// <summary>Counts the opacity and transparency groups of a page's annotations and their normal appearances.</summary>
    /// <param name="page">The page.</param>
    private void ScanAnnotationTransparency(PdfPage page)
    {
        var annotations = page.Dictionary.GetArray(KnownName.Annots);
        for (var i = 0; annotations is not null && i < annotations.Count; i++)
        {
            if (annotations.GetDictionary(i) is not { } annotation)
            {
                continue;
            }

            _alphas += IsBelowOne(annotation.Get(KnownName.CA)) ? 1 : 0;
            foreach (var appearance in NormalAppearances(annotation))
            {
                _groups += HasTransparencyGroup(appearance.Dictionary) ? 1 : 0;
            }
        }
    }

    /// <summary>Gets an annotation's normal appearance streams: the stream, or one per state.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The streams.</returns>
    private List<PdfStream> NormalAppearances(PdfDictionary annotation)
    {
        var streams = new List<PdfStream>();
        var normal = StoreReading.Resolve(_document.Objects, annotation.GetDictionary(KnownName.AP)?.GetRaw(KnownName.N) ?? default);
        if (normal.AsStream() is { } stream)
        {
            streams.Add(stream);
        }
        else if (normal.AsDictionary() is { } states)
        {
            for (var i = 0; i < states.Count; i++)
            {
                if (StoreReading.Resolve(_document.Objects, states.GetValueAt(i)).AsStream() is { } state)
                {
                    streams.Add(state);
                }
            }
        }

        return streams;
    }
}
