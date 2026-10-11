// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Windows.Input;
using PdfViewerLite.App.ViewModels;
using ReactiveUI.Primitives;

namespace PdfViewerLite.App.Tests;

/// <summary>Checks page selection, editing, history and file operations.</summary>
public sealed class PageManagementViewModelTests
{
    /// <summary>The initial page count in the edit scenarios.</summary>
    private const int InitialPages = 4;

    /// <summary>The page count in the smaller selection scenarios.</summary>
    private const int ThreePages = 3;

    /// <summary>The page count in the file-operation scenario.</summary>
    private const int TwoPages = 2;

    /// <summary>The page inserted by each import scenario.</summary>
    private const int ImportedPages = 1;

    /// <summary>The first page index.</summary>
    private const int FirstPage = 0;

    /// <summary>The second page index.</summary>
    private const int SecondPage = 1;

    /// <summary>The third page index.</summary>
    private const int ThirdPage = 2;

    /// <summary>The selected page used by most edit scenarios.</summary>
    private const int SelectedPage = 1;

    /// <summary>Pages selected to verify normalization.</summary>
    private static readonly int[] UnnormalizedSelection = [TwoPages, 0, TwoPages, -1, ThreePages];

    /// <summary>Pages selected to verify the thumbnail selection binding.</summary>
    private static readonly int[] ThumbnailSelection = [SecondPage, FirstPage];

    /// <summary>Pages selected to verify deletion guards.</summary>
    private static readonly int[] AllPagesSelection = [FirstPage, SecondPage, ThirdPage];

    /// <summary>Pages selected for extraction.</summary>
    private static readonly int[] ExtractSelection = [FirstPage, ThirdPage];

    /// <summary>All page operations that support history and saving.</summary>
    private static readonly PageOperation[] PageOperations =
    [
        PageOperation.RotateClockwise,
        PageOperation.RotateCounterclockwise,
        PageOperation.Duplicate,
        PageOperation.Delete,
        PageOperation.MoveEarlier,
        PageOperation.MoveLater,
        PageOperation.Insert,
        PageOperation.Merge,
    ];

    /// <summary>The engines used to reopen each saved edit.</summary>
    private static readonly TestEngineChoice[] ReadEngines = [TestEngineChoice.HyperPdf, TestEngineChoice.Pdfium];

    /// <summary>Gets every page mutation covered by undo, redo and save.</summary>
    /// <returns>The mutation cases.</returns>
    public static IEnumerable<PageOperation> Operations() => PageOperations;

    /// <summary>Verifies that selection is normalized and destructive commands protect the last page.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SelectionIsNormalizedAndLastPageCannotBeDeleted()
    {
        using var test = new TestServices(TestEngineChoice.HyperPdf);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("pages.pdf", ThreePages)]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        using var pages = new PageManagementViewModel(tab);

        pages.SelectPages(UnnormalizedSelection);
        var selected = pages.SelectedPages.ToArray();
        var canDeleteSome = ((ICommand)pages.DeleteCommand).CanExecute(null);
        pages.SelectedThumbnails = new object[] { tab.Thumbnails[ThumbnailSelection[0]], tab.Thumbnails[ThumbnailSelection[1]] };
        var fromThumbnailControl = pages.SelectedPages.ToArray();
        pages.SelectPages(AllPagesSelection);
        var canDeleteAll = ((ICommand)pages.DeleteCommand).CanExecute(null);

        using (Assert.Multiple())
        {
            await Assert.That(selected).IsEquivalentTo([FirstPage, TwoPages]);
            await Assert.That(fromThumbnailControl).IsEquivalentTo([FirstPage, SecondPage]);
            await Assert.That(canDeleteSome).IsTrue();
            await Assert.That(canDeleteAll).IsFalse();
            await Assert.That(pages.Status).Contains($"{ThreePages} pages selected");
        }
    }

    /// <summary>Verifies rotations, duplication, deletion and undo history update the open page structure.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EditCommandsUpdatePagesAndUndoHistory()
    {
        using var test = new TestServices(TestEngineChoice.HyperPdf);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("edit-pages.pdf", ThreePages)]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        using var pages = new PageManagementViewModel(tab);
        pages.SelectPages([SelectedPage]);

        _ = await pages.RotateClockwiseCommand.Execute().ToTask();
        pages.SelectPages([SelectedPage]);
        _ = await pages.DuplicateCommand.Execute().ToTask();
        var afterDuplicate = tab.PageCount;
        var canUndo = pages.CanUndo;
        _ = await pages.UndoCommand.Execute().ToTask();
        var afterUndo = tab.PageCount;
        _ = await pages.RedoCommand.Execute().ToTask();
        var afterRedo = tab.PageCount;
        pages.SelectPages([SelectedPage]);
        _ = await pages.DeleteCommand.Execute().ToTask();
        var afterDelete = tab.PageCount;

        using (Assert.Multiple())
        {
            await Assert.That(afterDuplicate).IsEqualTo(InitialPages);
            await Assert.That(canUndo).IsTrue();
            await Assert.That(pages.UndoText).Contains("Undo");
            await Assert.That(afterUndo).IsEqualTo(ThreePages);
            await Assert.That(afterRedo).IsEqualTo(InitialPages);
            await Assert.That(afterDelete).IsEqualTo(ThreePages);
        }
    }

    /// <summary>Verifies drag and command moves keep the moved block selected at its new position.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MoveCommandsSelectTheMovedPages()
    {
        using var test = new TestServices(TestEngineChoice.HyperPdf);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("move-pages.pdf", InitialPages)]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        using var pages = new PageManagementViewModel(tab);
        pages.SelectPages([ThirdPage]);

        _ = await pages.MoveEarlierCommand.Execute().ToTask();
        var earlier = pages.SelectedPages.ToArray();
        _ = await pages.MoveLaterCommand.Execute().ToTask();
        var later = pages.SelectedPages.ToArray();
        await pages.MoveAsync(0);
        var dragged = pages.SelectedPages.ToArray();

        using (Assert.Multiple())
        {
            await Assert.That(earlier).IsEquivalentTo([SecondPage]);
            await Assert.That(later).IsEquivalentTo([ThirdPage]);
            await Assert.That(dragged).IsEquivalentTo([FirstPage]);
        }
    }

    /// <summary>Verifies every mutation supports undo, redo, save and reopening in both engines.</summary>
    /// <param name="operation">The page mutation under test.</param>
    /// <returns>A task.</returns>
    [Test]
    [MethodDataSource(nameof(Operations))]
    public async Task MutationsUndoRedoAndSaveAcrossEngines(PageOperation operation)
    {
        using var test = new TestServices(TestEngineChoice.HyperPdf);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("mutation.pdf", InitialPages)]);
        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        using var pages = new PageManagementViewModel(tab);
        var import = test.CreateDocument("import.pdf", ImportedPages);
        using var insert = pages.InsertInteraction.RegisterHandler(context => context.SetOutput([import]));
        using var merge = pages.MergeFilesInteraction.RegisterHandler(context => context.SetOutput([import]));
        pages.SelectPages([GetSelectedPage(operation)]);
        await ApplyMutationAsync(operation, pages);
        var expectedPages = ExpectedPageCount(operation);
        var canUndo = pages.CanUndo;
        var savedPath = Path.Combine(test.Directory, $"saved-{operation}.pdf");
        _ = await pages.UndoCommand.Execute().ToTask();
        var pageCountAfterUndo = tab.PageCount;
        var canRedo = pages.CanRedo;
        _ = await pages.RedoCommand.Execute().ToTask();
        var pageCountAfterRedo = tab.PageCount;
        var saved = tab.Save(savedPath);
        var reopenedPageCounts = await ReopenSavedDocumentAsync(savedPath);

        using (Assert.Multiple())
        {
            await Assert.That(canUndo).IsTrue();
            await Assert.That(pageCountAfterUndo).IsEqualTo(InitialPages);
            await Assert.That(canRedo).IsTrue();
            await Assert.That(pageCountAfterRedo).IsEqualTo(expectedPages);
            await Assert.That(saved).IsTrue();
            await Assert.That(reopenedPageCounts).IsEquivalentTo([expectedPages, expectedPages]);
        }
    }

    /// <summary>Verifies insert, merge and extract use the selected files and preserve the page counts.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FileCommandsInsertMergeAndExtractPages()
    {
        using var test = new TestServices(TestEngineChoice.HyperPdf);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("file-pages.pdf", TwoPages)]);
        var tab = main.SelectedTab!;
        using var pages = new PageManagementViewModel(tab);
        var source = test.CreateDocument("source-pages.pdf", 1);
        using var insert = pages.InsertInteraction.RegisterHandler(context => context.SetOutput([source]));

        _ = await pages.InsertFilesCommand.Execute().ToTask();
        var afterInsert = tab.PageCount;
        using var merge = pages.MergeFilesInteraction.RegisterHandler(context => context.SetOutput([source]));
        _ = await pages.MergeFilesCommand.Execute().ToTask();
        var afterMerge = tab.PageCount;
        var destination = Path.Combine(test.Directory, "extracted-pages.pdf");
        pages.SelectPages(ExtractSelection);
        using var extract = pages.ExtractInteraction.RegisterHandler(context => context.SetOutput(destination));

        _ = await pages.ExtractCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(afterInsert).IsEqualTo(ThreePages);
            await Assert.That(afterMerge).IsEqualTo(InitialPages);
            await Assert.That(File.Exists(destination)).IsTrue();
            await Assert.That(new FileInfo(destination).Length).IsGreaterThan(0);
            await Assert.That(tab.PageCount).IsEqualTo(InitialPages);
        }
    }

    /// <summary>Verifies the app explains why page operations are unavailable on PDFium.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UnsupportedEngineExplainsPageEditingRequirement()
    {
        using var test = new TestServices(TestEngineChoice.Pdfium);
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("unsupported-pages.pdf", TwoPages)]);
        using var pages = new PageManagementViewModel(main.SelectedTab!);

        using (Assert.Multiple())
        {
            await Assert.That(pages.CanEdit).IsFalse();
            await Assert.That(pages.Status).Contains("HyperPDF");
            await Assert.That(((ICommand)pages.InsertFilesCommand).CanExecute(null)).IsFalse();
        }
    }

    /// <summary>Gets a starting page that permits the requested move.</summary>
    /// <param name="operation">The page mutation.</param>
    /// <returns>The selected page index.</returns>
    private static int GetSelectedPage(PageOperation operation) => operation switch
    {
        PageOperation.MoveEarlier => ThirdPage,
        PageOperation.MoveLater => FirstPage,
        _ => SelectedPage,
    };

    /// <summary>Runs one page mutation.</summary>
    /// <param name="operation">The page mutation.</param>
    /// <param name="pages">The page-management view model.</param>
    /// <returns>A task.</returns>
    private static async Task ApplyMutationAsync(PageOperation operation, PageManagementViewModel pages)
    {
        switch (operation)
        {
            case PageOperation.RotateClockwise:
            {
                _ = await pages.RotateClockwiseCommand.Execute().ToTask();
                break;
            }

            case PageOperation.RotateCounterclockwise:
            {
                _ = await pages.RotateCounterclockwiseCommand.Execute().ToTask();
                break;
            }

            case PageOperation.Duplicate:
            {
                _ = await pages.DuplicateCommand.Execute().ToTask();
                break;
            }

            case PageOperation.Delete:
            {
                _ = await pages.DeleteCommand.Execute().ToTask();
                break;
            }

            case PageOperation.MoveEarlier:
            {
                _ = await pages.MoveEarlierCommand.Execute().ToTask();
                break;
            }

            case PageOperation.MoveLater:
            {
                _ = await pages.MoveLaterCommand.Execute().ToTask();
                break;
            }

            case PageOperation.Insert:
            {
                _ = await pages.InsertFilesCommand.Execute().ToTask();
                break;
            }

            case PageOperation.Merge:
            {
                _ = await pages.MergeFilesCommand.Execute().ToTask();
                break;
            }
        }
    }

    /// <summary>Gets the page count after an operation.</summary>
    /// <param name="operation">The page mutation.</param>
    /// <returns>The resulting page count.</returns>
    private static int ExpectedPageCount(PageOperation operation) => operation switch
    {
        PageOperation.Delete => ThreePages,
        PageOperation.Duplicate or PageOperation.Insert or PageOperation.Merge => InitialPages + ImportedPages,
        _ => InitialPages,
    };

    /// <summary>Reopens a saved PDF through each supported engine.</summary>
    /// <param name="path">The saved PDF path.</param>
    /// <returns>The reopened page counts.</returns>
    private static async Task<int[]> ReopenSavedDocumentAsync(string path)
    {
        var pageCounts = new int[ReadEngines.Length];
        for (var index = 0; index < ReadEngines.Length; index++)
        {
            using var reader = new TestServices(ReadEngines[index]);
            using var readerMain = new MainViewModel(reader.Services);
            readerMain.Open([path]);
            var tab = readerMain.SelectedTab!;
            await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
            pageCounts[index] = tab.PageCount;
        }

        return pageCounts;
    }
}
