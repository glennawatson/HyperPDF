// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using System.Text;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Interchange;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Interchange;

/// <summary>Documents with annotations of every subtype the interchange formats carry. Coordinates are given as text.</summary>
internal static class InterchangeSamples
{
    /// <summary>The number of annotations <see cref="AddAnnotations"/> adds.</summary>
    internal const int AnnotationCount = 16;

    /// <summary>The name of the highlight that replies answer.</summary>
    internal const string HighlightName = "highlight-1";

    /// <summary>The attached file's text.</summary>
    internal const string AttachedText = "Attached bytes";

    /// <summary>A yellow as 0xRRGGBB.</summary>
    private const uint Yellow = 0xFFFF00;

    /// <summary>A red as 0xRRGGBB.</summary>
    private const uint Red = 0xFF0000;

    /// <summary>A green as 0xRRGGBB.</summary>
    private const uint Green = 0x00FF00;

    /// <summary>The year of the sample dates.</summary>
    private const int Year = 2026;

    /// <summary>The month of the sample dates.</summary>
    private const int Month = 1;

    /// <summary>The day of the sample dates.</summary>
    private const int Day = 2;

    /// <summary>The numbers in a point.</summary>
    private const int PointNumbers = 2;

    /// <summary>The line width of the shapes.</summary>
    private const float Width = 2;

    /// <summary>The opacity of the highlight.</summary>
    private const float Opacity = 0.5F;

    /// <summary>The cloud intensity of the polygon.</summary>
    private const float Intensity = 2;

    /// <summary>The justification of the free text: centred.</summary>
    private const int Centred = 1;

    /// <summary>The rectangle of every sample annotation.</summary>
    private const string Box = "72,600,172,650";

    /// <summary>The corners of a quadrilateral over the rectangle.</summary>
    private const string Quad = "72,650;172,650;72,600;172,600";

    /// <summary>The rectangle of a markup annotation's pop-up.</summary>
    private const string PopupBox = "200,600,300,700";

    /// <summary>The points of the two ink strokes, three and two points.</summary>
    private const string InkPoints = "80,610;90,620;100,610;110,640;120,600";

    /// <summary>The three vertices of the polygons.</summary>
    private const string Triangle = "80,610;160,610;120,640";

    /// <summary>The end points of the line.</summary>
    private const string LineEnds = "80,610,160,640";

    /// <summary>The dash pattern of the shapes.</summary>
    private const string Dashes = "3,2";

    /// <summary>The numbers of points in the ink strokes.</summary>
    private static readonly int[] StrokeLengths = [3, 2];

    /// <summary>Adds <see cref="AnnotationCount"/> annotations to the first two pages of a document of at least two pages.</summary>
    /// <param name="document">The document.</param>
    internal static void AddAnnotations(PdfDocument document)
    {
        var store = document.Objects;
        var first = PdfDocumentPages.GetPage(document, 0);
        var second = PdfDocumentPages.GetPage(document, 1);
        var highlight = Add(store, first, Markup(store, KnownName.Highlight, Yellow, HighlightName));
        _ = Add(store, first, Markup(store, KnownName.Underline, Red, "underline-1"));
        _ = Add(store, first, Markup(store, KnownName.StrikeOut, Red, "strikeout-1"));
        _ = Add(store, first, Markup(store, KnownName.Squiggly, Green, "squiggly-1"));
        _ = Add(store, first, Shape(store, KnownName.Square, "square-1"));
        _ = Add(store, first, Shape(store, KnownName.Circle, "circle-1"));
        _ = Add(store, first, Ink(store));
        _ = Add(store, first, Text(store, "note-1"));
        var reply = Text(store, "reply-1");
        PdfAnnotations.SetInReplyTo(reply, highlight);
        PdfAnnotations.SetText(reply, store.Names.Intern("Subj"), "Reply subject");
        _ = Add(store, first, reply);
        _ = Add(store, first, Poly(store, KnownName.Polygon, "polygon-1"));
        _ = Add(store, second, Poly(store, KnownName.PolyLine, "polyline-1"));
        _ = Add(store, second, Line(store));
        _ = Add(store, second, FreeText(store));
        _ = Add(store, second, Stamp(store));
        _ = Add(store, second, Caret(store));
        _ = Add(store, second, Attachment(store));
    }

    /// <summary>Reads a rectangle from text.</summary>
    /// <param name="text">The four numbers.</param>
    /// <returns>The rectangle.</returns>
    internal static PdfRectangle Rect(string text) => InterchangeValues.TryParseRect(text, out var rectangle) ? rectangle : default;

    /// <summary>Appends an annotation to a page.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="page">The page.</param>
    /// <param name="annotation">The annotation.</param>
    /// <returns>The annotation's object id.</returns>
    private static PdfObjectId Add(PdfObjectStore store, PdfPage page, PdfDictionary annotation)
    {
        var index = PdfPageAnnotations.Append(store, page, annotation);
        return PdfPageAnnotations.GetId(store, page, index);
    }

    /// <summary>Encodes text as ASCII.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The bytes.</returns>
    private static byte[] Bytes(string text) => Encoding.ASCII.GetBytes(text);

    /// <summary>Reads points written as <c>x,y;x,y</c>.</summary>
    /// <param name="text">The text.</param>
    /// <returns>The points.</returns>
    private static Vector2[] Points(string text)
    {
        var numbers = InterchangeValues.ParseList(text);
        var points = new Vector2[numbers.Length / PointNumbers];
        for (var i = 0; i < points.Length; i++)
        {
            points[i] = new(numbers[i * PointNumbers], numbers[(i * PointNumbers) + 1]);
        }

        return points;
    }

    /// <summary>Sets what every sample annotation has.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <param name="name">The unique name.</param>
    private static void Describe(PdfDictionary annotation, string name)
    {
        PdfAnnotations.SetText(annotation, KnownName.NM, name);
        PdfAnnotations.SetText(annotation, KnownName.T, "Glenn");
        PdfAnnotations.SetText(annotation, KnownName.Contents, $"Contents of {name}\nsecond line");
        PdfAnnotations.SetFlags(annotation, PdfAnnotationFlags.Print);
        PdfAnnotations.SetDate(annotation, KnownName.M, new(Year, Month, Day, Day, Month, Day, TimeSpan.Zero));
    }

    /// <summary>Makes a text markup annotation with a pop-up.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="subtype">The subtype.</param>
    /// <param name="color">The colour.</param>
    /// <param name="name">The name.</param>
    /// <returns>The annotation.</returns>
    private static PdfDictionary Markup(PdfObjectStore store, PdfName subtype, uint color, string name)
    {
        var annotation = PdfAnnotations.Create(store, subtype, Rect(Box));
        Describe(annotation, name);
        PdfAnnotations.SetColor(annotation, KnownName.C, color);
        PdfAnnotations.SetOpacity(annotation, Opacity);
        PdfAnnotations.SetQuadPoints(annotation, Points(Quad));
        var popup = PdfAnnotations.Create(store, KnownName.Popup, Rect(PopupBox));
        popup.Set(KnownName.Open, PdfValue.FromBoolean(true));
        annotation.Set(KnownName.Popup, PdfValue.FromDictionary(popup));
        return annotation;
    }

    /// <summary>Makes a square or circle.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="subtype">The subtype.</param>
    /// <param name="name">The name.</param>
    /// <returns>The annotation.</returns>
    private static PdfDictionary Shape(PdfObjectStore store, PdfName subtype, string name)
    {
        var annotation = PdfAnnotations.Create(store, subtype, Rect(Box));
        Describe(annotation, name);
        PdfAnnotations.SetColor(annotation, KnownName.C, Red);
        PdfAnnotations.SetColor(annotation, KnownName.IC, Green);
        var style = new PdfDictionary(store);
        style.Set(KnownName.W, PdfNumber.ToValue(Width));
        style.Set(KnownName.S, PdfValue.FromName(KnownName.D));
        style.Set(KnownName.D, PdfValue.FromArray(PdfArray.FromNumbers(store, InterchangeValues.ParseList(Dashes))));
        annotation.Set(KnownName.BS, PdfValue.FromDictionary(style));
        return annotation;
    }

    /// <summary>Makes an ink annotation with two strokes.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>The annotation.</returns>
    private static PdfDictionary Ink(PdfObjectStore store)
    {
        var annotation = PdfAnnotations.Create(store, KnownName.Ink, Rect(Box));
        Describe(annotation, "ink-1");
        PdfAnnotations.SetColor(annotation, KnownName.C, Red);
        PdfAnnotations.SetInkList(annotation, Points(InkPoints), StrokeLengths);
        return annotation;
    }

    /// <summary>Makes a text note.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="name">The name.</param>
    /// <returns>The annotation.</returns>
    private static PdfDictionary Text(PdfObjectStore store, string name)
    {
        var annotation = PdfAnnotations.Create(store, KnownName.Text, Rect(Box));
        Describe(annotation, name);
        annotation.Set(KnownName.Name, PdfValue.FromName(store.Names.Intern("Comment")));
        PdfAnnotations.SetText(annotation, KnownName.RC, "<body xmlns=\"http://www.w3.org/1999/xhtml\"><p>Rich <b>text</b></p></body>");
        return annotation;
    }

    /// <summary>Makes a polygon or polyline.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="subtype">The subtype.</param>
    /// <param name="name">The name.</param>
    /// <returns>The annotation.</returns>
    private static PdfDictionary Poly(PdfObjectStore store, PdfName subtype, string name)
    {
        var annotation = PdfAnnotations.Create(store, subtype, Rect(Box));
        Describe(annotation, name);
        PdfAnnotations.SetPoints(annotation, KnownName.Vertices, Points(Triangle));
        PdfAnnotations.SetColor(annotation, KnownName.IC, Yellow);
        var effect = new PdfDictionary(store);
        effect.Set(KnownName.S, PdfValue.FromName(KnownName.C));
        effect.Set(KnownName.I, PdfNumber.ToValue(Intensity));
        annotation.Set(KnownName.BE, PdfValue.FromDictionary(effect));
        return annotation;
    }

    /// <summary>Makes a line with an arrow head.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>The annotation.</returns>
    private static PdfDictionary Line(PdfObjectStore store)
    {
        var annotation = PdfAnnotations.Create(store, KnownName.Line, Rect(Box));
        Describe(annotation, "line-1");
        annotation.Set(KnownName.L, PdfValue.FromArray(PdfArray.FromNumbers(store, InterchangeValues.ParseList(LineEnds))));
        var endings = new PdfArray(store);
        endings.Add(PdfValue.FromName(KnownName.OpenArrow));
        endings.Add(PdfValue.FromName(KnownName.NoneName));
        annotation.Set(KnownName.LE, PdfValue.FromArray(endings));
        return annotation;
    }

    /// <summary>Makes a free text annotation.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>The annotation.</returns>
    private static PdfDictionary FreeText(PdfObjectStore store)
    {
        var annotation = PdfAnnotations.Create(store, KnownName.FreeText, Rect(Box));
        Describe(annotation, "freetext-1");
        PdfAnnotations.SetText(annotation, KnownName.DA, "/Helv 12 Tf 0 g");
        PdfAnnotations.SetText(annotation, KnownName.DS, "font: 12pt Helvetica");
        annotation.Set(KnownName.Q, PdfValue.FromInteger(Centred));
        PdfAnnotations.SetText(annotation, KnownName.RC, "<body xmlns=\"http://www.w3.org/1999/xhtml\"><p>Free <i>text</i></p></body>");
        return annotation;
    }

    /// <summary>Makes a stamp.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>The annotation.</returns>
    private static PdfDictionary Stamp(PdfObjectStore store)
    {
        var annotation = PdfAnnotations.Create(store, KnownName.Stamp, Rect(Box));
        Describe(annotation, "stamp-1");
        annotation.Set(KnownName.Name, PdfValue.FromName(store.Names.Intern("Approved")));
        return annotation;
    }

    /// <summary>Makes a caret.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>The annotation.</returns>
    private static PdfDictionary Caret(PdfObjectStore store)
    {
        var annotation = PdfAnnotations.Create(store, KnownName.Caret, Rect(Box));
        Describe(annotation, "caret-1");
        annotation.Set(store.Names.Intern("Sy"), PdfValue.FromName(KnownName.P));
        return annotation;
    }

    /// <summary>Makes a file attachment.</summary>
    /// <param name="store">The document's objects.</param>
    /// <returns>The annotation.</returns>
    private static PdfDictionary Attachment(PdfObjectStore store)
    {
        var annotation = PdfAnnotations.Create(store, KnownName.FileAttachment, Rect(Box));
        Describe(annotation, "attachment-1");
        annotation.Set(KnownName.Name, PdfValue.FromName(store.Names.Intern("PushPin")));
        var file = new PdfDictionary(store);
        file.Set(KnownName.Type, PdfValue.FromName(store.Names.Intern("EmbeddedFile")));
        var embedded = new PdfDictionary(store);
        embedded.Set(KnownName.F, PdfValue.FromReference(StoreEditing.Add(store, PdfValue.FromStream(new(file, Bytes(AttachedText))))));
        var spec = new PdfDictionary(store);
        spec.Set(KnownName.Type, PdfValue.FromName(KnownName.Filespec));
        PdfAnnotations.SetText(spec, KnownName.F, "attached.txt");
        PdfAnnotations.SetText(spec, KnownName.UF, "attached.txt");
        spec.Set(KnownName.EF, PdfValue.FromDictionary(embedded));
        annotation.Set(store.Names.Intern("FS"), PdfValue.FromDictionary(spec));
        return annotation;
    }
}
