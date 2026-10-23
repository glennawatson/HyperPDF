// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Rendering;

namespace HyperPdfLibrary.Tests.Rendering;

/// <summary>Checks that text and file attachment annotations without an appearance draw the icon their name asks for.</summary>
public sealed class AnnotationIconRenderTests
{
    /// <summary>The page width and height in points, and so in pixels at scale 1.</summary>
    private const int Size = 100;

    /// <summary>The subtype of a text annotation.</summary>
    private const string TextSubtype = "Text";

    /// <summary>The subtype of a file attachment annotation.</summary>
    private const string AttachmentSubtype = "FileAttachment";

    /// <summary>The rectangle every test annotation has.</summary>
    private const string Rectangle = "/Rect [10 10 60 60]";

    /// <summary>The column inside the push pin's head.</summary>
    private const int PinColumn = 20;

    /// <summary>The row inside the push pin's head, counted from the top of the page.</summary>
    private const int PinRow = 46;

    /// <summary>How close a channel must be to count as the expected colour.</summary>
    private const int Tolerance = 12;

    /// <summary>The red channel of the push pin's head, 0.85 of 255.</summary>
    private const int PinRed = 217;

    /// <summary>The green and blue channels of the push pin's head, 0.1 of 255.</summary>
    private const int PinOther = 26;

    /// <summary>The icons a text annotation can name.</summary>
    private static readonly string[] NoteNames = ["Comment", "Note", "Key", "Help", "Paragraph", "NewParagraph", "Insert"];

    /// <summary>The icons a file attachment annotation can name.</summary>
    private static readonly string[] AttachmentNames = ["PushPin", "Graph", "Paperclip", "Tag"];

    /// <summary>Verifies that every text annotation name draws a different icon, and that an unknown name draws the speech box.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task TextAnnotationNames_DrawDifferentIcons()
    {
        var images = NoteNames.Select(static name => Render(TextSubtype, name)).ToArray();

        for (var i = 0; i < images.Length; i++)
        {
            await Assert.That(HasInk(images[i])).IsTrue();
            for (var j = i + 1; j < images.Length; j++)
            {
                await Assert.That(images[i].Pixels.AsSpan().SequenceEqual(images[j].Pixels)).IsFalse();
            }
        }

        await Assert.That(Render(TextSubtype, "NoSuchIcon").Pixels.AsSpan().SequenceEqual(images[0].Pixels)).IsTrue();
        await Assert.That(Render(TextSubtype, null).Pixels.AsSpan().SequenceEqual(images[0].Pixels)).IsTrue();
    }

    /// <summary>Verifies that every file attachment name draws a different icon, and that the default is the push pin.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task FileAttachmentNames_DrawDifferentIcons()
    {
        var images = AttachmentNames.Select(static name => Render(AttachmentSubtype, name)).ToArray();

        for (var i = 0; i < images.Length; i++)
        {
            await Assert.That(HasInk(images[i])).IsTrue();
            for (var j = i + 1; j < images.Length; j++)
            {
                await Assert.That(images[i].Pixels.AsSpan().SequenceEqual(images[j].Pixels)).IsFalse();
            }
        }

        await Assert.That(Render(AttachmentSubtype, null).Pixels.AsSpan().SequenceEqual(images[0].Pixels)).IsTrue();
    }

    /// <summary>Verifies that the push pin has a red head.</summary>
    /// <returns>A task representing the asynchronous test.</returns>
    [Test]
    public async Task PushPin_HasARedHead()
    {
        var image = Render(AttachmentSubtype, "PushPin");

        await RenderCheck.Near(image, PinColumn, PinRow, new(PinRed, PinOther, PinOther), Tolerance, nameof(PushPin_HasARedHead));
    }

    /// <summary>Renders a page with one annotation.</summary>
    /// <param name="subtype">The annotation's subtype.</param>
    /// <param name="name">The annotation's <c>/Name</c>, or <see langword="null"/> for none.</param>
    /// <returns>The pixels.</returns>
    private static RenderedImage Render(string subtype, string? name)
    {
        var pdf = new RenderTestPdf(Size, Size);
        var nameEntry = name is null ? string.Empty : $" /Name /{name}";
        var annotation = pdf.AddObject($"<< /Type /Annot /Subtype /{subtype} {Rectangle}{nameEntry} >>");
        pdf.PageEntries = $"/Annots [{annotation} 0 R]";
        using var page = new RenderTestPage(pdf.ToBytes());
        return page.RenderPage(1, 0, PdfRenderFlags.Annotations);
    }

    /// <summary>Determines whether an image has any pixel that is not the white of the page.</summary>
    /// <param name="image">The image.</param>
    /// <returns><see langword="true"/> when something was drawn.</returns>
    private static bool HasInk(RenderedImage image)
    {
        foreach (var value in image.Pixels)
        {
            if (value != byte.MaxValue)
            {
                return true;
            }
        }

        return false;
    }
}
