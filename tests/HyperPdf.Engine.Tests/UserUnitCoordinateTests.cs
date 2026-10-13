// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Geometry;
using HyperPdf.Engine.Tests.PdfiumReference;
using HyperPdfLibrary.Tests.Rendering;

namespace HyperPdf.Engine.Tests;

/// <summary>
/// Checks that every viewer-facing coordinate is in physical points (user space times <c>/UserUnit</c>, ISO 32000-2 7.7.3.3):
/// a page with a user unit of 2 and half-size coordinates must read exactly like the same page with a user unit of 1.
/// </summary>
public sealed class UserUnitCoordinateTests
{
    /// <summary>The physical page edge in points.</summary>
    private const int PhysicalEdge = 400;

    /// <summary>The user unit of the page under test.</summary>
    private const int Unit = 2;

    /// <summary>The largest difference accepted between two readings of the same physical place, in points.</summary>
    private const float Tolerance = 0.6F;

    /// <summary>The text drawn on the page.</summary>
    private const string Word = "HELLO";

    /// <summary>The physical font size of the text.</summary>
    private const int PhysicalFontSize = 24;

    /// <summary>The physical left edge of the text.</summary>
    private const int TextLeft = 40;

    /// <summary>The physical baseline of the text, measured from the page bottom.</summary>
    private const int TextBaseline = 300;

    /// <summary>The object number the page dictionary has.</summary>
    private const int PageObject = 3;

    /// <summary>The colour the new rectangle is drawn in.</summary>
    private const uint Colour = 0xFF0000;

    /// <summary>The physical width of the square's border.</summary>
    private const int SquareBorder = 4;

    /// <summary>The physical width given to the rectangle that is added.</summary>
    private const float DrawnWidth = 6;

    /// <summary>The allowed difference for a line width, in points.</summary>
    private const float WidthTolerance = 0.01F;

    /// <summary>The square annotation, bottom-up in physical points.</summary>
    private static readonly Place Square = new(40, 40, 160, 120);

    /// <summary>The link annotation, bottom-up in physical points.</summary>
    private static readonly Place Link = new(200, 200, 300, 240);

    /// <summary>The text field widget, bottom-up in physical points.</summary>
    private static readonly Place Field = new(200, 40, 360, 80);

    /// <summary>The corner where the added rectangle's drag starts, top-down in physical points.</summary>
    private static readonly PagePoint DragStart = new(100, 60);

    /// <summary>The corner where the added rectangle's drag ends, top-down in physical points.</summary>
    private static readonly PagePoint DragEnd = new(220, 140);

    /// <summary>Annotations, links and form widgets report physical bounds.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AnnotationLinkAndFieldBoundsArePhysical()
    {
        using var unit = new HyperPdfTestDocument(Build(Unit));
        var annotations = new List<PageAnnotation>();
        Editor(unit).GetAnnotations(0, annotations);
        var links = unit.Document.GetLinks(0);
        var fields = new List<FormField>();
        DocumentFeature<IFormFiller>.Cast(unit.Document)!.GetFields(0, fields);

        await Assert.That(annotations[0].Bounds).IsEqualTo(Square.TopDown());
        await Assert.That(annotations[0].LineWidth).IsEqualTo((float)SquareBorder);
        await Assert.That(links[0].Bounds).IsEqualTo(Link.TopDown());
        await Assert.That(fields[0].Bounds).IsEqualTo(Field.TopDown());
    }

    /// <summary>A user unit of 2 with half-size coordinates reads the same as a user unit of 1.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadingsMatchTheUnitOnePage()
    {
        using var doubled = new HyperPdfTestDocument(Build(Unit));
        using var plain = new HyperPdfTestDocument(Build(1));
        var a = new List<PageAnnotation>();
        var b = new List<PageAnnotation>();
        Editor(doubled).GetAnnotations(0, a);
        Editor(plain).GetAnnotations(0, b);

        await Assert.That(doubled.Document.GetPageSizes()[0]).IsEqualTo(plain.Document.GetPageSizes()[0]);
        await Assert.That(a[0].Bounds).IsEqualTo(b[0].Bounds);
        await Assert.That(a[0].LineWidth).IsEqualTo(b[0].LineWidth);
        await Assert.That(doubled.Document.GetLinks(0)[0].Bounds).IsEqualTo(plain.Document.GetLinks(0)[0].Bounds);
    }

    /// <summary>A text selection and a hit test use physical points.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TextSelectionAndHitTestMatchTheUnitOnePage()
    {
        using var doubled = new HyperPdfTestDocument(Build(Unit));
        using var plain = new HyperPdfTestDocument(Build(1));
        var a = new List<PageRect>();
        var b = new List<PageRect>();
        doubled.Document.GetTextBounds(0, 0, Word.Length, a);
        plain.Document.GetTextBounds(0, 0, Word.Length, b);

        await Assert.That(a.Count).IsEqualTo(b.Count);
        await Assert.That(a[0].Left).IsEqualTo(b[0].Left).Within(Tolerance);
        await Assert.That(a[0].Top).IsEqualTo(b[0].Top).Within(Tolerance);
        await Assert.That(a[0].Width).IsEqualTo(b[0].Width).Within(Tolerance);
        await Assert.That(a[0].Height).IsEqualTo(b[0].Height).Within(Tolerance);

        var inside = new PagePoint(a[0].Left + (a[0].Width / Unit), a[0].Top + (a[0].Height / Unit));
        var hit = doubled.Document.GetCharacterIndexAt(0, inside, 1);
        await Assert.That(hit).IsEqualTo(plain.Document.GetCharacterIndexAt(0, inside, 1));
        await Assert.That(hit).IsGreaterThanOrEqualTo(0);
        await Assert.That(doubled.Document.GetCharacterIndexAt(0, new(PhysicalEdge - 1, PhysicalEdge - 1), 1)).IsEqualTo(-1);
    }

    /// <summary>An annotation created at physical points reads back at the same points and keeps its line width.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CreatedAnnotationRoundTripsInPhysicalPoints()
    {
        using var unit = new HyperPdfTestDocument(Build(Unit));
        var editor = Editor(unit);
        var index = editor.AddShape(0, AnnotationKind.Rectangle, DragStart, DragEnd, Colour, DrawnWidth);
        var annotations = new List<PageAnnotation>();
        editor.GetAnnotations(0, annotations);
        var added = annotations.Single(annotation => annotation.Index == index);

        await Assert.That(added.LineWidth).IsEqualTo(DrawnWidth).Within(WidthTolerance);
        await Assert.That(added.Bounds.Left).IsLessThanOrEqualTo(DragStart.X);
        await Assert.That(added.Bounds.Left).IsGreaterThan(DragStart.X - DrawnWidth);
        await Assert.That(added.Bounds.Right).IsGreaterThanOrEqualTo(DragEnd.X);
        await Assert.That(added.Bounds.Right).IsLessThan(DragEnd.X + DrawnWidth);
        await Assert.That(added.Bounds.Top).IsLessThanOrEqualTo(DragStart.Y);
        await Assert.That(added.Bounds.Bottom).IsGreaterThanOrEqualTo(DragEnd.Y);
    }

    /// <summary>The link rectangle is the reference reader's user space rectangle scaled by the user unit; the reference ignores the unit.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LinkBoundsAreTheReferenceRectangleTimesTheUnit()
    {
        using var pair = new EnginePair(Build(Unit));
        var reference = PdfiumReferenceLinks.GetLinks(pair.Pdfium, 0)[0].Bounds;
        var ours = pair.HyperPdf.GetLinks(0)[0].Bounds;
        var userEdge = PhysicalEdge / Unit;

        await Assert.That(ours.Left).IsEqualTo(reference.Left * Unit).Within(Tolerance);
        await Assert.That(ours.Right).IsEqualTo(reference.Right * Unit).Within(Tolerance);
        await Assert.That(ours.Top).IsEqualTo((userEdge - reference.Top) * Unit).Within(Tolerance);
        await Assert.That(ours.Bottom).IsEqualTo((userEdge - reference.Bottom) * Unit).Within(Tolerance);
    }

    /// <summary>Gets the annotation editor of a document.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The editor.</returns>
    private static IAnnotationEditor Editor(HyperPdfTestDocument document) => DocumentFeature<IAnnotationEditor>.Cast(document.Document)!;

    /// <summary>
    /// Builds a page that is 400 points wide and high physically, with a text word, a square, a link and a text field, all at
    /// the same physical places whatever the unit.
    /// </summary>
    /// <param name="unit">The page's <c>/UserUnit</c>.</param>
    /// <returns>The PDF bytes.</returns>
    private static byte[] Build(int unit)
    {
        var edge = PhysicalEdge / unit;
        var pdf = new RenderTestPdf(edge, edge)
        {
            Resources = "/Font << /F1 << /Type /Font /Subtype /Type1 /BaseFont /Helvetica >> >>",
            Content = string.Create(CultureInfo.InvariantCulture, $"BT /F1 {PhysicalFontSize / unit} Tf {TextLeft / unit} {TextBaseline / unit} Td ({Word}) Tj ET"),
        };
        var square = pdf.AddObject(
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /Annot /Subtype /Square /Rect [{Square.In(unit)}] /C [1 0 0] /Border [0 0 {SquareBorder / unit}] /F 4 >>"));
        var link = pdf.AddObject(
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /Annot /Subtype /Link /Rect [{Link.In(unit)}] /Border [0 0 0] /A << /S /URI /URI (https://example.com/) >> >>"));
        var field = pdf.AddObject(
            string.Create(CultureInfo.InvariantCulture, $"<< /Type /Annot /Subtype /Widget /FT /Tx /T (name) /V (hi) /Rect [{Field.In(unit)}] /P {PageObject} 0 R /F 4 >>"));
        pdf.PageEntries = string.Create(CultureInfo.InvariantCulture, $"/UserUnit {unit} /Annots [{square} 0 R {link} 0 R {field} 0 R]");
        pdf.CatalogEntries = string.Create(CultureInfo.InvariantCulture, $"/AcroForm << /Fields [{field} 0 R] /DA (/Helv 0 Tf 0 g) >>");
        return pdf.ToBytes();
    }

    /// <summary>A rectangle in physical points with the origin at the page's bottom left.</summary>
    /// <param name="Left">The left edge.</param>
    /// <param name="Bottom">The bottom edge.</param>
    /// <param name="Right">The right edge.</param>
    /// <param name="Top">The top edge.</param>
    private readonly record struct Place(int Left, int Bottom, int Right, int Top)
    {
        /// <summary>Gets the rectangle as the viewer reports it: origin at the top left, Y down.</summary>
        /// <returns>The page rectangle.</returns>
        internal PageRect TopDown() => new(Left, PhysicalEdge - Top, Right - Left, Top - Bottom);

        /// <summary>Formats the rectangle in user space units.</summary>
        /// <param name="unit">The user unit.</param>
        /// <returns>The four numbers.</returns>
        internal string In(int unit) => string.Create(CultureInfo.InvariantCulture, $"{Left / unit} {Bottom / unit} {Right / unit} {Top / unit}");
    }
}
