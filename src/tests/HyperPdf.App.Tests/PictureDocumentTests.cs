// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.ViewModels;
using HyperPdfLibrary.Export;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Checks that pictures open as one page documents and are never overwritten by a save.</summary>
public sealed class PictureDocumentTests
{
    /// <summary>The picture's width in pixels.</summary>
    private const int PictureWidth = 40;

    /// <summary>The picture's height in pixels.</summary>
    private const int PictureHeight = 20;

    /// <summary>The bytes in a BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>The points in a pixel of a picture of unknown resolution.</summary>
    private const float PointsPerPixel = 0.75F;

    /// <summary>The size error allowed, in points.</summary>
    private const float Tolerance = 0.01F;

    /// <summary>The blue, green and red of the picture's opaque red pixels, and their alpha.</summary>
    private static readonly byte[] RedPixel = [0, 0, 255, 255];

    /// <summary>Picture endings are told from PDFs, whatever their case.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecognisesPictureEndings()
    {
        using (Assert.Multiple())
        {
            await Assert.That(PictureFiles.IsPictureFile("/data/scan.PNG")).IsTrue();
            await Assert.That(PictureFiles.IsPictureFile("photo.jpeg")).IsTrue();
            await Assert.That(PictureFiles.IsPictureFile("fax.tiff")).IsTrue();
            await Assert.That(PictureFiles.IsPictureFile("book.pdf")).IsFalse();
            await Assert.That(PictureFiles.SuggestedPdfName("scan.png")).IsEqualTo("scan.pdf");
        }
    }

    /// <summary>A PNG opens as a one page document sized after its pixels, and saving asks for a PDF name instead of replacing the picture.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task APictureOpensAsOnePageAndIsNeverOverwritten()
    {
        using var test = new TestServices();
        var path = Path.Combine(test.Directory, "scan.png");
        await File.WriteAllBytesAsync(path, CreatePng());
        var before = await File.ReadAllBytesAsync(path);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [path]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        string? suggested = null;
        using var handler = tab.SaveAsInteraction.RegisterHandler(context =>
        {
            suggested = context.Input;
            context.SetOutput(null);
        });

        _ = await tab.SaveCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(tab.Source.PageSizes.Length).IsEqualTo(1);
            await Assert.That(tab.Source.PageSizes[0].Width).IsEqualTo(PictureWidth * PointsPerPixel).Within(Tolerance);
            await Assert.That(tab.Source.PageSizes[0].Height).IsEqualTo(PictureHeight * PointsPerPixel).Within(Tolerance);
            await Assert.That(suggested).IsEqualTo("scan.pdf");
            await Assert.That(await File.ReadAllBytesAsync(path)).IsEquivalentTo(before);
        }
    }

    /// <summary>Makes a small red PNG.</summary>
    /// <returns>The file.</returns>
    private static byte[] CreatePng()
    {
        var pixels = new byte[PictureWidth * PictureHeight * BytesPerPixel];
        for (var offset = 0; offset < pixels.Length; offset += BytesPerPixel)
        {
            RedPixel.CopyTo(pixels, offset);
        }

        return PdfRasterFiles.Encode(PdfRasterFiles.FromPremultipliedBgra(pixels, PictureWidth, PictureHeight), PdfRasterFileFormat.Png, CancellationToken.None);
    }
}
