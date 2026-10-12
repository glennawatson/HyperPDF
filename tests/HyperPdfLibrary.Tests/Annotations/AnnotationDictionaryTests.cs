// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Numerics;
using HyperPdf.TestAssets;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Tests.Annotations;

/// <summary>Tests for <see cref="PdfAnnotations"/>: the entries of annotation dictionaries.</summary>
public sealed class AnnotationDictionaryTests
{
    /// <summary>A soft red as 0xRRGGBB.</summary>
    private const uint Clay = 0xE8BCB4;

    /// <summary>A line width.</summary>
    private const float Width = 2.5F;

    /// <summary>A number kept as text.</summary>
    private const float Size = 13.5F;

    /// <summary>The points in the first stroke.</summary>
    private const int FirstStroke = 3;

    /// <summary>The points in the second stroke.</summary>
    private const int SecondStroke = 2;

    /// <summary>A gray level.</summary>
    private const float Gray = 0.5F;

    /// <summary>The gray level as 0xRRGGBB.</summary>
    private const uint GrayColor = 0x808080;

    /// <summary>The stroke lengths of the ink list.</summary>
    private static readonly int[] Lengths = [FirstStroke, SecondStroke];

    /// <summary>The channels of <see cref="Clay"/>.</summary>
    private static readonly byte[] ClayChannels = [0xE8, 0xBC, 0xB4];

    /// <summary>A rectangle.</summary>
    private static readonly PdfRectangle Box = new(10, 20, 110, 70);

    /// <summary>Some points.</summary>
    private static readonly Vector2[] Points = [new(1, 2), new(3, 4), new(5, 6), new(7, 8), new(9, 10)];

    /// <summary>A colour is rounded up at the sixth decimal so truncating readers get the byte back, and reads back exactly.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ColorsRoundTrip()
    {
        using var store = Open();
        var annotation = PdfAnnotations.Create(store, KnownName.Square, Box);
        PdfAnnotations.SetColor(annotation, KnownName.C, Clay);
        var array = annotation.GetArray(KnownName.C)!;
        var gray = new PdfDictionary(store);
        gray.Set(KnownName.C, PdfValue.FromArray(PdfArray.FromNumbers(store, [Gray])));
        await Assert.That(PdfAnnotations.TryGetColor(annotation, KnownName.C, out var color)).IsTrue();
        await Assert.That(color).IsEqualTo(Clay);
        var truncated = new byte[array.Count];
        for (var i = 0; i < truncated.Length; i++)
        {
            truncated[i] = (byte)(array.GetSingle(i) * byte.MaxValue);
        }

        await Assert.That(truncated).IsEquivalentTo(ClayChannels);
        await Assert.That(PdfAnnotations.TryGetColor(gray, KnownName.C, out var grayColor)).IsTrue();
        await Assert.That(grayColor).IsEqualTo(GrayColor);
        await Assert.That(PdfAnnotations.GetRectangle(annotation)).IsEqualTo(Box);
        await Assert.That(annotation.IsName(KnownName.Type, KnownName.Annot)).IsTrue();
    }

    /// <summary>Borders, flags, text, dates and numbers kept as text read back.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EntriesRoundTrip()
    {
        using var store = Open();
        var annotation = PdfAnnotations.Create(store, KnownName.Ink, Box);
        var key = store.Names.Intern("PVLFontSize"u8);
        var subject = store.Names.Intern("Subj"u8);
        var date = new DateTimeOffset(2026, 10, 9, 12, 30, 15, TimeSpan.Zero);
        PdfAnnotations.SetBorderWidth(annotation, Width);
        PdfAnnotations.SetFlags(annotation, PdfAnnotationFlags.Print | PdfAnnotationFlags.Hidden);
        PdfAnnotations.SetText(annotation, KnownName.T, "Zoë");
        PdfAnnotations.SetText(annotation, subject, "Arrow");
        PdfAnnotations.SetDate(annotation, KnownName.M, date);
        PdfAnnotations.SetNumberText(annotation, key, Size);
        await Assert.That(PdfAnnotations.GetBorderWidth(annotation)).IsEqualTo(Width);
        await Assert.That(PdfAnnotations.GetFlags(annotation)).IsEqualTo(PdfAnnotationFlags.Print | PdfAnnotationFlags.Hidden);
        await Assert.That(PdfAnnotations.GetText(annotation, KnownName.T)).IsEqualTo("Zoë");
        await Assert.That(PdfAnnotations.TextEquals(annotation, subject, "Arrow"u8)).IsTrue();
        await Assert.That(PdfAnnotations.TextEquals(annotation, subject, "Line"u8)).IsFalse();
        await Assert.That(PdfAnnotations.GetDate(annotation, KnownName.M)).IsEqualTo(date);
        await Assert.That(PdfAnnotations.GetNumberText(annotation, key)).IsEqualTo(Size);
        await Assert.That(PdfAnnotations.HasText(annotation, KnownName.Contents)).IsFalse();
    }

    /// <summary>Ink lists and flat point arrays read back stroke by stroke.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PointsRoundTrip()
    {
        using var store = Open();
        var annotation = PdfAnnotations.Create(store, KnownName.Ink, Box);
        PdfAnnotations.SetInkList(annotation, Points, Lengths);
        PdfAnnotations.SetPoints(annotation, KnownName.Vertices, Points);
        var read = ReadBack(annotation);
        await Assert.That(read.Lengths).IsEquivalentTo(Lengths);
        await Assert.That(read.Ink).IsEquivalentTo(Points);
        await Assert.That(read.Vertices).IsEquivalentTo(Points);
        await Assert.That(read.Bounds).IsEqualTo(new(Points[0].X, Points[0].Y, Points[^1].X, Points[^1].Y));
    }

    /// <summary>A reply refers to its comment and records a review state as text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RepliesReferToTheirComment()
    {
        using var store = Open();
        var comment = PdfAnnotations.Create(store, KnownName.Text, Box);
        var id = StoreEditing.Add(store, PdfValue.FromDictionary(comment));
        var reply = PdfAnnotations.Create(store, KnownName.Text, Box);
        PdfAnnotations.SetInReplyTo(reply, id);
        PdfAnnotations.SetReviewState(reply, "Accepted"u8);
        await Assert.That(PdfAnnotations.GetInReplyTo(reply)).IsSameReferenceAs(comment);
        await Assert.That(reply.IsName(KnownName.RT, KnownName.R)).IsTrue();
        await Assert.That(PdfAnnotations.TextEquals(reply, KnownName.State, "Accepted"u8)).IsTrue();
        await Assert.That(PdfAnnotations.TextEquals(reply, KnownName.StateModel, "Review"u8)).IsTrue();
    }

    /// <summary>Opens a one page document.</summary>
    /// <returns>The objects.</returns>
    private static PdfObjectStore Open() => StoreOpening.Open(TestPdf.Create(1), null);

    /// <summary>Reads an annotation's ink list and vertices back, outside any await.</summary>
    /// <param name="annotation">The annotation.</param>
    /// <returns>What was read.</returns>
    private static PointsRead ReadBack(PdfDictionary annotation)
    {
        var ink = default(PdfStrokeBuffer);
        var vertices = default(PdfStrokeBuffer);
        try
        {
            _ = PdfAnnotations.ReadInkList(annotation, ref ink);
            _ = PdfAnnotations.ReadPoints(annotation, KnownName.Vertices, ref vertices);
            return new(ink.Points.ToArray(), ink.Lengths.ToArray(), vertices.Points.ToArray(), vertices.GetBounds());
        }
        finally
        {
            ink.Dispose();
            vertices.Dispose();
        }
    }

    /// <summary>Points read back from an annotation.</summary>
    /// <param name="Ink">The ink list's points.</param>
    /// <param name="Lengths">The ink list's stroke lengths.</param>
    /// <param name="Vertices">The vertices.</param>
    /// <param name="Bounds">The vertices' bounds.</param>
    private sealed record PointsRead(Vector2[] Ink, int[] Lengths, Vector2[] Vertices, PdfRectangle Bounds);
}
