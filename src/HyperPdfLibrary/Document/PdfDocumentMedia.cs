// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Media;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads document media entries.</summary>
public static class PdfDocumentMedia
{
    /// <summary>The deepest chain of clip sections followed.</summary>
    private const int MaxClipDepth = 8;

    /// <summary>The default sampling rate of a sound, in samples per second.</summary>
    private const double DefaultSoundRate = 44_100;

    /// <summary>The default bits per sample of a sound.</summary>
    private const int DefaultSoundBits = 8;

    /// <summary>The play mode of a movie that names none.</summary>
    private const string DefaultMovieMode = "Once";

    /// <summary>The sound encoding that a sound names none for.</summary>
    private const string DefaultSoundEncoding = "Raw";

    /// <summary>Gets the multimedia annotations (screen, movie, sound, rich media and 3D) of a page.</summary>
    /// <param name="document">The document.</param>
    /// <param name="page">The page.</param>
    /// <returns>The annotations in page order.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="page"/> is null.</exception>
    public static PdfMultimediaAnnotation[] GetMultimediaAnnotations(PdfDocument document, PdfPage page)
    {
        ArgumentNullException.ThrowIfNull(page);
        var annots = page.Dictionary.GetArray(KnownName.Annots);
        var result = new List<PdfMultimediaAnnotation>();
        for (var i = 0; annots is not null && i < annots.Count; i++)
        {
            if (annots.GetDictionary(i) is { } annot && PdfDocumentMedia.MultimediaKindOf(annot.GetName(KnownName.Subtype).ToKnownName()) is { } kind)
            {
                result.Add(PdfDocumentMedia.ReadMultimedia(document, annot, kind, page.Index));
            }
        }

        return [.. result];
    }

    /// <summary>Gets the multimedia annotations of every page.</summary>
    /// <param name="document">The document.</param>
    /// <returns>An immutable snapshot of the annotations in document order, read once until the next edit.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static PdfMultimediaAnnotation[] GetMultimediaAnnotations(PdfDocument document) =>
        PdfReadSnapshots.Read(document, PdfReadKind.MultimediaAnnotations, PdfDocumentMedia.ReadMultimediaAnnotations);

    /// <summary>Reads the multimedia annotations of every page.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The annotations in document order.</returns>
    internal static PdfMultimediaAnnotation[] ReadMultimediaAnnotations(PdfDocument document)
    {
        var result = new List<PdfMultimediaAnnotation>();
        foreach (var page in PdfDocumentPages.GetPageSet(document).Pages)
        {
            result.AddRange(PdfDocumentMedia.GetMultimediaAnnotations(document, page));
        }

        return [.. result];
    }

    /// <summary>Reads a sound stream.</summary>
    /// <param name="stream">The sound stream.</param>
    /// <returns>The sound.</returns>
    internal static PdfSound ReadSound(PdfStream stream)
    {
        var dictionary = stream.Dictionary;
        return new(
dictionary.Num("R", PdfDocumentMedia.DefaultSoundRate),
dictionary.Int("C", 1),
dictionary.Int("B", PdfDocumentMedia.DefaultSoundBits),
dictionary.NameText("E") ?? PdfDocumentMedia.DefaultSoundEncoding,
dictionary.NameText("CO"),
stream);
    }

    /// <summary>Reads a rendition dictionary.</summary>
    /// <param name="document">The document.</param>
    /// <param name="rendition">The rendition dictionary.</param>
    /// <returns>The rendition.</returns>
    internal static PdfRendition ReadRendition(PdfDocument document, PdfDictionary rendition)
    {
        var clip = rendition.Dict("C");
        return new(
rendition.NameText("S") ?? string.Empty,
rendition.Text("N"),
clip is null ? null : PdfDocumentMedia.ReadMediaClip(document, clip, 0),
rendition.Dict("P"),
rendition.Dict("SP"),
rendition.Dict("MH"),
rendition.Dict("BE"));
    }

    /// <summary>Reads a media clip, following clip sections to the clip they are sections of.</summary>
    /// <param name="document">The document.</param>
    /// <param name="clip">The clip dictionary.</param>
    /// <param name="depth">The depth of the section chain.</param>
    /// <returns>The clip.</returns>
    internal static PdfMediaClip ReadMediaClip(PdfDocument document, PdfDictionary clip, int depth)
    {
        var subtype = clip.NameText("S") ?? "MCD";
        var data = clip.Value("D");
        if (subtype == "MCS")
        {
            var parent = depth < PdfDocumentMedia.MaxClipDepth ? data.AsDictionary() : null;
            return new(subtype, clip.Text("N"), null, clip.Text("CT"), null, null, parent is null ? null : PdfDocumentMedia.ReadMediaClip(document, parent, depth + 1));
        }

        return new(
subtype,
clip.Text("N"),
data.AsStream() is null ? PdfDocumentFileSpecs.ReadFileSpec(document, data) : null,
clip.Text("CT"),
PdfDocumentMedia.ClipStream(data),
clip.Dict("P")?.Text("TF"),
null);
    }

    /// <summary>Converts an array of numbers to integers.</summary>
    /// <param name="array">The array, or null.</param>
    /// <returns>The integers; empty when the array is missing.</returns>
    internal static int[] ToInt32Array(PdfArray? array)
    {
        if (array is null)
        {
            return [];
        }

        var values = new int[array.Count];
        for (var i = 0; i < values.Length; i++)
        {
            values[i] = array.GetInt32(i);
        }

        return values;
    }

    /// <summary>Gets the media stream of a clip's <c>/D</c> value: the stream itself, or the embedded file of a file specification.</summary>
    /// <param name="data">The <c>/D</c> value.</param>
    /// <returns>The stream, or null when the data is external.</returns>
    private static PdfStream? ClipStream(PdfValue data)
    {
        var embedded = data.AsDictionary()?.GetDictionary(KnownName.EF);
        return data.AsStream() ?? embedded?.GetStream(KnownName.UF) ?? embedded?.GetStream(KnownName.F);
    }

    /// <summary>Gets the multimedia kind of an annotation subtype.</summary>
    /// <param name="subtype">The subtype.</param>
    /// <returns>The kind, or null when the annotation is not multimedia.</returns>
    private static PdfMultimediaKind? MultimediaKindOf(KnownName subtype) => subtype switch
    {
        KnownName.Screen => PdfMultimediaKind.Screen,
        KnownName.Movie => PdfMultimediaKind.Movie,
        KnownName.Sound => PdfMultimediaKind.Sound,
        KnownName.RichMedia => PdfMultimediaKind.RichMedia,
        KnownName.ThreeD => PdfMultimediaKind.ThreeD,
        _ => null,
    };

    /// <summary>Reads a sound annotation's sound.</summary>
    /// <param name="annot">The annotation dictionary.</param>
    /// <returns>The sound, or null when the annotation has none.</returns>
    private static PdfSound? ReadSoundAnnotation(PdfDictionary annot) => annot.Stream("Sound") is { } sound ? PdfDocumentMedia.ReadSound(sound) : null;

    /// <summary>Reads a multimedia annotation of a known kind.</summary>
    /// <param name="document">The document.</param>
    /// <param name="annot">The annotation dictionary.</param>
    /// <param name="kind">The kind.</param>
    /// <param name="pageIndex">The page.</param>
    /// <returns>The annotation.</returns>
    private static PdfMultimediaAnnotation ReadMultimedia(PdfDocument document, PdfDictionary annot, PdfMultimediaKind kind, int pageIndex)
    {
        PdfRectangle? bounds = annot.TryGetRectangle(KnownName.Rect, out var rect) ? rect : null;
        var empty = new PdfMultimediaAnnotation(kind, pageIndex, bounds, null, null, null, null, null);
        return kind switch
        {
            PdfMultimediaKind.Screen => empty with
            {
                Screen = PdfDocumentMedia.ReadScreen(document, annot)
            },
            PdfMultimediaKind.Movie => empty with
            {
                Movie = PdfDocumentMedia.ReadMovie(document, annot)
            },
            PdfMultimediaKind.Sound => empty with
            {
                Sound = PdfDocumentMedia.ReadSoundAnnotation(annot)
            },
            PdfMultimediaKind.RichMedia => empty with
            {
                RichMedia = PdfDocumentMedia3D.ReadRichMedia(document, annot)
            },
            _ => empty with
            {
                ThreeD = PdfDocumentMedia3D.Read3D(document, annot)
            },
        };
    }

    /// <summary>Reads a screen annotation.</summary>
    /// <param name="document">The document.</param>
    /// <param name="annot">The annotation dictionary.</param>
    /// <returns>The screen data.</returns>
    private static PdfScreenAnnotation ReadScreen(PdfDocument document, PdfDictionary annot) => new(
annot.GetText(KnownName.T),
annot.GetDictionary(KnownName.A) is { } action ? PdfDocumentActions.ReadActionNode(document, action) : null,
[.. PdfDocumentActions.GetTriggers(document, annot)]);

    /// <summary>Reads a movie annotation.</summary>
    /// <param name="document">The document.</param>
    /// <param name="annot">The annotation dictionary.</param>
    /// <returns>The movie data.</returns>
    private static PdfMovieAnnotation ReadMovie(PdfDocument document, PdfDictionary annot)
    {
        var movie = annot.Dict("Movie");
        var poster = movie?.Value("Poster") ?? default;
        return new(
annot.GetText(KnownName.T),
movie is null ? null : PdfDocumentFileSpecs.ReadFileSpec(document, movie.Get(KnownName.F)),
[.. PdfDocumentMedia.ToInt32Array(movie?.Array("Aspect"))],
movie?.Int("Rotate", 0) ?? 0,
!poster.IsNull && poster.AsBoolean(true),
PdfDocumentMedia.ReadMovieActivation(document, annot.Dict("A")));
    }

    /// <summary>Reads movie activation parameters; a missing or boolean value gives the defaults.</summary>
    /// <param name="document">The document.</param>
    /// <param name="activation">The <c>/A</c> dictionary, or null.</param>
    /// <returns>The activation.</returns>
    private static PdfMovieActivation ReadMovieActivation(PdfDocument document, PdfDictionary? activation) => activation is null ? new(
null,
null,
1,
1,
false,
PdfDocumentMedia.DefaultMovieMode,
false,
[],
[]) : new(
activation.Value("Start").ScalarText(document.Objects),
activation.Value("Duration").ScalarText(document.Objects),
activation.Num("Rate", 1),
activation.Num("Volume", 1),
activation.Flag("ShowControls", false),
activation.NameText("Mode") ?? PdfDocumentMedia.DefaultMovieMode,
activation.Flag("Synchronous", false),
[.. PdfDocumentMedia.ToInt32Array(activation.Array("FWScale"))],
[.. activation.Array("FWPosition").Numbers()]);
}
