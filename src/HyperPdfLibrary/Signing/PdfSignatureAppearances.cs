// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Globalization;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Signatures;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Signing;

/// <summary>Draws the appearance stream of a visible signature: the signer, reason, location and date, and an optional image.</summary>
internal static class PdfSignatureAppearances
{
    /// <summary>The most lines the default text has: signer, reason, location and date.</summary>
    private const int DefaultLines = 4;

    /// <summary>The characters of the description that are not the signer, reason or location: the fixed words, the time and the commas.</summary>
    private const int DescriptionOverhead = 128;

    /// <summary>The description size built on the stack; a longer one uses a pooled array.</summary>
    private const int StackCharacters = 256;

    /// <summary>The format of the time in the description.</summary>
    private const string DescriptionTimeFormat = "yyyy-MM-dd HH:mm";

    /// <summary>The smallest font size the text shrinks to before it is clipped.</summary>
    private const float MinimumFontSize = 4;

    /// <summary>The step the font size shrinks by.</summary>
    private const float FontStep = 0.5F;

    /// <summary>The space between the text and the edge, in points.</summary>
    private const float Margin = 2;

    /// <summary>The number of margins across the box: one on each side.</summary>
    private const float Sides = 2;

    /// <summary>The divisor that halves a length.</summary>
    private const float Half = 2;

    /// <summary>The leading as a multiple of the font size.</summary>
    private const float Leading = 1.2F;

    /// <summary>The gray of the border.</summary>
    private const float BorderGray = 0.55F;

    /// <summary>The width of the border in points.</summary>
    private const float BorderWidth = 0.5F;

    /// <summary>Builds the lines of text a signature shows.</summary>
    /// <param name="plan">The plan.</param>
    /// <returns>The lines.</returns>
    internal static List<string> Lines(PdfSigningPlan plan)
    {
        var appearance = plan.Appearance;
        if (appearance.Lines is not null)
        {
            return [.. appearance.Lines];
        }

        var lines = new List<string>(DefaultLines);
        if (appearance.ShowName && plan.SignerName.Length > 0)
        {
            lines.Add($"Digitally signed by {plan.SignerName}");
        }

        if (appearance.ShowReason && plan.Reason is { } reason)
        {
            lines.Add($"Reason: {reason}");
        }

        if (appearance.ShowLocation && plan.Location is { } location)
        {
            lines.Add($"Location: {location}");
        }

        if (appearance.ShowDate)
        {
            lines.Add(string.Create(CultureInfo.InvariantCulture, $"Date: {plan.Time:yyyy.MM.dd HH:mm:ss zzz}"));
        }

        return lines;
    }

    /// <summary>Describes a signature in a sentence for a screen reader, as a PDF text string.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="allowUtf8">Whether the document is PDF 2.0 or higher, so the text may be UTF-8.</param>
    /// <returns>The encoded description.</returns>
    internal static byte[] Describe(PdfSigningPlan plan, bool allowUtf8)
    {
        if (plan.Appearance.Description is { Length: > 0 } given)
        {
            return PdfText.Encode(given, allowUtf8);
        }

        // The sentence is built in a stack or pooled buffer and encoded from there, so only the encoded bytes are allocated.
        var capacity = DescriptionOverhead + plan.SignerName.Length + (plan.Reason?.Length ?? 0) + (plan.Location?.Length ?? 0);
        var rented = capacity > StackCharacters ? ArrayPool<char>.Shared.Rent(capacity) : null;
        try
        {
            Span<char> buffer = rented is null ? stackalloc char[StackCharacters] : rented;
            var writer = new CharWriter(buffer);
            WriteDescription(ref writer, plan);
            return PdfText.Encode(writer.Written, allowUtf8);
        }
        finally
        {
            if (rented is not null)
            {
                ArrayPool<char>.Shared.Return(rented);
            }
        }
    }

    /// <summary>Adds the appearance stream to the document.</summary>
    /// <param name="document">The document.</param>
    /// <param name="transaction">The transaction.</param>
    /// <param name="plan">The plan; its appearance is visible.</param>
    /// <returns>The form XObject's object id.</returns>
    internal static PdfObjectId Add(PdfDocument document, PdfEditTransaction transaction, PdfSigningPlan plan)
    {
        var store = document.Objects;
        var appearance = plan.Appearance;
        var size = new BoxSize(appearance.Rectangle.Width, appearance.Rectangle.Height);
        var resources = new PdfDictionary(store);
        var builder = default(PdfContentBuilder);
        try
        {
            builder.SaveState();
            DrawBorder(ref builder, size);
            var lines = appearance.HasText ? Lines(plan) : [];
            var textLeft = DrawImage(store, ref builder, resources, appearance, size, lines.Count > 0);
            if (lines.Count > 0)
            {
                DrawText(store, ref builder, resources, appearance, lines, new(textLeft, size.Width - textLeft - Margin, size.Height));
            }

            builder.RestoreState();
            return transaction.Add(PdfValue.FromStream(builder.ToFormXObject(store, new(0, 0, size.Width, size.Height), resources)));
        }
        finally
        {
            builder.Dispose();
        }
    }

    /// <summary>Writes the sentence that describes a signature.</summary>
    /// <param name="writer">The writer.</param>
    /// <param name="plan">The plan.</param>
    private static void WriteDescription(ref CharWriter writer, PdfSigningPlan plan)
    {
        if (plan.Format == PdfSignatureFormat.Rfc3161)
        {
            writer.Append("Document timestamp, added on ");
            writer.Append(plan.Time, DescriptionTimeFormat);
            writer.Append('.');
            return;
        }

        writer.Append(plan.IsCertification ? "Certification signature" : "Signature");
        if (plan.SignerName.Length > 0)
        {
            writer.Append(", signed by ");
            writer.Append(plan.SignerName);
        }

        if (plan.Reason is { } reason)
        {
            writer.Append(", reason: ");
            writer.Append(reason);
        }

        if (plan.Location is { } location)
        {
            writer.Append(", location: ");
            writer.Append(location);
        }

        writer.Append(", on ");
        writer.Append(plan.Time, DescriptionTimeFormat);
        writer.Append('.');
    }

    /// <summary>Draws a thin border so an empty-looking signature still shows where it is.</summary>
    /// <param name="builder">The content.</param>
    /// <param name="size">The size of the box.</param>
    private static void DrawBorder(ref PdfContentBuilder builder, BoxSize size)
    {
        builder.SetLineWidth(BorderWidth);
        builder.SetStrokeGray(BorderGray);
        builder.Rectangle(BorderWidth / Half, BorderWidth / Half, size.Width - BorderWidth, size.Height - BorderWidth);
        builder.Stroke();
    }

    /// <summary>Draws the image, if there is one.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="builder">The content.</param>
    /// <param name="resources">The resources, which receive the image.</param>
    /// <param name="appearance">The appearance.</param>
    /// <param name="size">The size of the box.</param>
    /// <param name="hasText">Whether text is drawn too, which takes the right half.</param>
    /// <returns>Where the text starts.</returns>
    private static float DrawImage(PdfObjectStore store, ref PdfContentBuilder builder, PdfDictionary resources, PdfSignatureAppearance appearance, BoxSize size, bool hasText)
    {
        if (appearance.Image is not { } image)
        {
            return Margin;
        }

        var area = hasText ? size.Width / Half : size.Width;
        var scale = Math.Min((area - (Sides * Margin)) / image.Width, (size.Height - (Sides * Margin)) / image.Height);
        var drawnWidth = image.Width * scale;
        var drawnHeight = image.Height * scale;
        var images = new PdfDictionary(store);
        images.Set(store.Names.Intern("SigImage"u8), PdfValue.FromReference(PdfImages.AddBgraImage(store, image.Pixels.Span, image.Width, image.Height)));
        resources.Set(KnownName.XObject, PdfValue.FromDictionary(images));
        builder.SaveState();
        builder.Transform(drawnWidth, 0, 0, drawnHeight, Margin + ((area - (Sides * Margin) - drawnWidth) / Half), (size.Height - drawnHeight) / Half);
        builder.DrawXObject("SigImage"u8);
        builder.RestoreState();
        return hasText ? area + Margin : Margin;
    }

    /// <summary>Draws the lines of text, shrinking the font until they fit.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="builder">The content.</param>
    /// <param name="resources">The resources, which receive the font.</param>
    /// <param name="appearance">The appearance.</param>
    /// <param name="lines">The lines.</param>
    /// <param name="area">Where the text goes.</param>
    private static void DrawText(PdfObjectStore store, ref PdfContentBuilder builder, PdfDictionary resources, PdfSignatureAppearance appearance, List<string> lines, TextArea area)
    {
        var font = PdfSignatureFont.Open(store, appearance);
        var codes = lines.ConvertAll(font.Encode);
        var wanted = appearance.FontSize > 0 ? appearance.FontSize : PdfSignatureAppearance.DefaultFontSize;
        var size = Fit(font, codes, wanted, area.Width, area.Height - (Sides * Margin));
        var fonts = new PdfDictionary(store);
        fonts.Set(store.Names.Intern("SigFont"u8), font.Value);
        resources.Set(KnownName.Font, PdfValue.FromDictionary(fonts));
        builder.BeginText();
        builder.SetFont("SigFont"u8, size);
        builder.SetFillGray(0);
        builder.SetLeading(size * Leading);
        builder.MoveText(area.Left, area.Height - Margin - (font.Ascent * size));
        for (var i = 0; i < codes.Count; i++)
        {
            if (i > 0)
            {
                builder.NextLine();
            }

            builder.ShowText(codes[i]);
        }

        builder.EndText();
    }

    /// <summary>Finds the largest font size, at most the wanted one, at which the lines fit the space.</summary>
    /// <param name="font">The font.</param>
    /// <param name="codes">The lines as codes.</param>
    /// <param name="wanted">The wanted size.</param>
    /// <param name="availableWidth">The width the text may take.</param>
    /// <param name="availableHeight">The height the text may take.</param>
    /// <returns>The size.</returns>
    private static float Fit(PdfSignatureFont font, List<byte[]> codes, float wanted, float availableWidth, float availableHeight)
    {
        var size = wanted;
        while (size > MinimumFontSize && !Fits(font, codes, size, availableWidth, availableHeight))
        {
            size -= FontStep;
        }

        return Math.Max(size, MinimumFontSize);
    }

    /// <summary>Determines whether the lines fit the space at a size.</summary>
    /// <param name="font">The font.</param>
    /// <param name="codes">The lines as codes.</param>
    /// <param name="size">The size.</param>
    /// <param name="availableWidth">The width the text may take.</param>
    /// <param name="availableHeight">The height the text may take.</param>
    /// <returns><see langword="true"/> when they fit.</returns>
    private static bool Fits(PdfSignatureFont font, List<byte[]> codes, float size, float availableWidth, float availableHeight)
    {
        var textHeight = ((codes.Count - 1) * size * Leading) + ((font.Ascent + font.Descent) * size);
        if (textHeight > availableHeight)
        {
            return false;
        }

        foreach (var line in codes)
        {
            if (font.Measure(line, size) > availableWidth)
            {
                return false;
            }
        }

        return true;
    }

    /// <summary>Appends text to a fixed buffer that is known to be large enough.</summary>
    private ref struct CharWriter
    {
        /// <summary>The buffer.</summary>
        private readonly Span<char> _buffer;

        /// <summary>The characters written.</summary>
        private int _length;

        /// <summary>Initializes a new instance of the <see cref="CharWriter"/> struct.</summary>
        /// <param name="buffer">The buffer, large enough for everything that is appended.</param>
        internal CharWriter(Span<char> buffer)
        {
            _buffer = buffer;
            _length = 0;
        }

        /// <summary>Gets the characters written.</summary>
        internal readonly ReadOnlySpan<char> Written => _buffer[.._length];

        /// <summary>Appends text.</summary>
        /// <param name="text">The text.</param>
        internal void Append(scoped ReadOnlySpan<char> text)
        {
            text.CopyTo(_buffer[_length..]);
            _length += text.Length;
        }

        /// <summary>Appends a character.</summary>
        /// <param name="value">The character.</param>
        internal void Append(char value)
        {
            _buffer[_length] = value;
            _length++;
        }

        /// <summary>Appends a time.</summary>
        /// <param name="time">The time.</param>
        /// <param name="format">The format.</param>
        internal void Append(DateTimeOffset time, string format)
        {
            _ = time.TryFormat(_buffer[_length..], out var written, format, CultureInfo.InvariantCulture);
            _length += written;
        }
    }

    /// <summary>The size of the signature box.</summary>
    /// <param name="Width">The width.</param>
    /// <param name="Height">The height.</param>
    private readonly record struct BoxSize(float Width, float Height);

    /// <summary>The area the text is drawn in.</summary>
    /// <param name="Left">Where the text starts across the box.</param>
    /// <param name="Width">The width the text may take.</param>
    /// <param name="Height">The height of the box.</param>
    private readonly record struct TextArea(float Left, float Width, float Height);
}
