// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Geometry;
using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>Tests showing and hiding form fields by name, as a form script's display setting asks.</summary>
public sealed class FormVisibilityTests
{
    /// <summary>The bytes in one BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The field the tests hide.</summary>
    private const string Field = "Quantity";

    /// <summary>Hiding a field changes the page that is drawn, showing it brings the drawing back, and a repeat changes nothing.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HidesAndShowsAFieldByName()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-visibility-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateScriptedForm());
        try
        {
            using var document = new HyperPdfEngine().Open(path, null);
            var visibility = DocumentFeature<IFormVisibility>.Cast(document)!;
            var shown = Draw(document);

            var hid = visibility.SetHidden(Field, true);
            var hidden = Draw(document);
            var hidAgain = visibility.SetHidden(Field, true);
            var showed = visibility.SetHidden(Field, false);
            var shownAgain = Draw(document);
            var missing = visibility.SetHidden("No such field", true);

            await Assert.That(hid).IsTrue();
            await Assert.That(hidden.AsSpan().SequenceEqual(shown)).IsFalse();
            await Assert.That(hidAgain).IsFalse();
            await Assert.That(showed).IsTrue();
            await Assert.That(shownAgain.AsSpan().SequenceEqual(shown)).IsTrue();
            await Assert.That(missing).IsFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Draws the first page with its form fields.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The pixels.</returns>
    private static byte[] Draw(IDocument document)
    {
        var size = document.GetPageSizes()[0];
        var width = (int)Math.Round(size.Width);
        var height = (int)Math.Round(size.Height);
        var pixels = new byte[width * height * BytesPerPixel];
        _ = document.Render(new(0, 1, PageRotation.None, 0, 0, RenderFlags.Annotations), new(pixels, width, height, width * BytesPerPixel));
        return pixels;
    }
}
