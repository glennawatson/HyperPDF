// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.ViewModels;
using HyperPdf.ViewModels.Output;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks that picture files become pages in the sidebar, as one undoable step.</summary>
public sealed class PageImagesViewModelTests
{
    /// <summary>The name given to the decoded test picture.</summary>
    private const string PictureName = "picture.png";

    /// <summary>The first picture file chosen.</summary>
    private const string FirstFile = "one.png";

    /// <summary>The pages of the starting document.</summary>
    private const int StartPages = 2;

    /// <summary>The width of the test picture in pixels.</summary>
    private const int PictureWidth = 4;

    /// <summary>The height of the test picture in pixels.</summary>
    private const int PictureHeight = 2;

    /// <summary>The pictures added in the first test.</summary>
    private const int AddedPictures = 2;

    /// <summary>The length of the file header checked, such as <c>%PDF-2.0</c>.</summary>
    private const int HeaderLength = 8;

    /// <summary>The bytes in one BGRA pixel.</summary>
    private const int BytesPerPixel = 4;

    /// <summary>Adds a page for each chosen picture after the selected page, and undo takes them away.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AddsAPagePerPictureAndUndoRemovesThem()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("picture-pages.pdf", StartPages)]);
        var tab = main.SelectedTab!;
        ((FakeRenderHead)test.Services.Head).Image = new(PictureName, new byte[PictureWidth * PictureHeight * BytesPerPixel], PictureWidth, PictureHeight);
        using var choose = tab.Pages.InsertImagesInteraction.RegisterHandler(static context => context.SetOutput([FirstFile, "two.png"]));
        tab.Pages.SelectPages([0]);

        _ = await tab.Pages.InsertImagesCommand.Execute().ToTask();
        var afterAdd = tab.PageCount;
        var selected = tab.Pages.SelectedPages.ToArray();
        _ = await tab.Pages.UndoCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(afterAdd).IsEqualTo(StartPages + AddedPictures);
            await Assert.That(selected).IsEquivalentTo([1, AddedPictures]);
            await Assert.That(tab.PageCount).IsEqualTo(StartPages);
        }
    }

    /// <summary>Turns picture files into a new PDF 2.0 file with a page for each.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ConvertsPicturesToANewPdf20File()
    {
        using var test = new TestServices();
        ((FakeRenderHead)test.Services.Head).Image = new(PictureName, new byte[PictureWidth * PictureHeight * BytesPerPixel], PictureWidth, PictureHeight);
        var destination = Path.Combine(test.Directory, "pictures.pdf");

        var pages = await PictureConversion.ConvertAsync(test.Services, [FirstFile, "two.png"], destination, CancellationToken.None);
        var header = new byte[HeaderLength];
        await using (var file = File.OpenRead(destination))
        {
            _ = await file.ReadAsync(header);
        }

        using (Assert.Multiple())
        {
            await Assert.That(pages).IsEqualTo(AddedPictures);
            await Assert.That(System.Text.Encoding.ASCII.GetString(header)).IsEqualTo("%PDF-2.0");
            await Assert.That(File.Exists($"{destination}.writing")).IsFalse();
        }
    }

    /// <summary>Saving after adding pages writes a PDF 2.0 file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavingAfterAddingPagesWritesPdf20()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("pages-save.pdf", StartPages)]);
        var tab = main.SelectedTab!;
        ((FakeRenderHead)test.Services.Head).Image = new(PictureName, new byte[PictureWidth * PictureHeight * BytesPerPixel], PictureWidth, PictureHeight);
        using var choose = tab.Pages.InsertImagesInteraction.RegisterHandler(static context => context.SetOutput([FirstFile]));
        _ = await tab.Pages.InsertImagesCommand.Execute().ToTask();
        var path = Path.Combine(test.Directory, "pages-saved.pdf");

        var saved = await tab.SaveAsync(path, CancellationToken.None);
        var header = new byte[HeaderLength];
        await using (var file = File.OpenRead(path))
        {
            _ = await file.ReadAsync(header);
        }

        using (Assert.Multiple())
        {
            await Assert.That(saved).IsTrue();
            await Assert.That(System.Text.Encoding.ASCII.GetString(header)).IsEqualTo("%PDF-2.0");
        }
    }

    /// <summary>Tells the user and leaves the document alone when no chosen file is a picture.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task LeavesTheDocumentAloneWhenNoFileIsAPicture()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("no-pictures.pdf", StartPages)]);
        var tab = main.SelectedTab!;
        ((FakeRenderHead)test.Services.Head).Image = null;
        using var choose = tab.Pages.InsertImagesInteraction.RegisterHandler(static context => context.SetOutput(["notes.txt"]));

        _ = await tab.Pages.InsertImagesCommand.Execute().ToTask();

        await Assert.That(tab.PageCount).IsEqualTo(StartPages);
    }
}
