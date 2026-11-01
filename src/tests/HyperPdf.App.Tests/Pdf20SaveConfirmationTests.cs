// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using Avalonia.Controls;
using HyperPdf.App.Views;
using HyperPdf.Core.Annotations;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Geometry;
using HyperPdf.Core.Protection;
using HyperPdf.Engine;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Checks, in the real window, that saving a protected file after editing asks first, and writes PDF 2.0 only when the person agrees.</summary>
public sealed class Pdf20SaveConfirmationTests
{
    /// <summary>The password that opens the protected test file.</summary>
    private const string OwnerPassword = "owner-pass";

    /// <summary>The length of the file header checked, such as <c>%PDF-2.0</c>.</summary>
    private const int HeaderLength = 8;

    /// <summary>The window asks, and OK saves a PDF 2.0 file with no protection.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OkSavesPdf20WithoutProtection()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var tab = await OpenEditedProtectedAsync(test, main, "confirm-ok.pdf");
        var window = new MainWindow { ViewModel = main };
        window.Show();
        try
        {
            await Assert.That(await UiWait.UntilAsync(() => window.IsLoaded)).IsTrue();
            var path = Path.Combine(test.Directory, "confirm-ok-saved.pdf");

            var saving = tab.SaveAsync(path, CancellationToken.None);
            var asked = await UiWait.UntilAsync(() => window.OwnedWindows.OfType<ConfirmWindow>().Any());
            window.OwnedWindows.OfType<ConfirmWindow>().Single().Close(true);
            var saved = await saving;

            using (Assert.Multiple())
            {
                await Assert.That(asked).IsTrue();
                await Assert.That(saved).IsTrue();
                await Assert.That(await HeaderOfAsync(path)).IsEqualTo("%PDF-2.0");
                await Assert.That(IsEncrypted(path)).IsFalse();
            }
        }
        finally
        {
            Close(window);
        }
    }

    /// <summary>Cancel saves the file as it was, and keeps its protection.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelKeepsTheFilesOwnVersionAndProtection()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        var tab = await OpenEditedProtectedAsync(test, main, "confirm-cancel.pdf");
        var window = new MainWindow { ViewModel = main };
        window.Show();
        try
        {
            await Assert.That(await UiWait.UntilAsync(() => window.IsLoaded)).IsTrue();
            var path = Path.Combine(test.Directory, "confirm-cancel-saved.pdf");

            var saving = tab.SaveAsync(path, CancellationToken.None);
            var asked = await UiWait.UntilAsync(() => window.OwnedWindows.OfType<ConfirmWindow>().Any());
            window.OwnedWindows.OfType<ConfirmWindow>().Single().Close(false);
            var saved = await saving;

            using (Assert.Multiple())
            {
                await Assert.That(asked).IsTrue();
                await Assert.That(saved).IsTrue();
                await Assert.That(IsEncrypted(path)).IsTrue();
            }
        }
        finally
        {
            Close(window);
        }
    }

    /// <summary>Closes a window and any window it owns.</summary>
    /// <param name="window">The window.</param>
    private static void Close(Window window)
    {
        foreach (var owned in window.OwnedWindows)
        {
            owned.Close(true);
        }

        window.Close();
    }

    /// <summary>Reads the first bytes of a file as text.</summary>
    /// <param name="path">The file.</param>
    /// <returns>The header, such as <c>%PDF-2.0</c>.</returns>
    private static async Task<string> HeaderOfAsync(string path)
    {
        var header = new byte[HeaderLength];
        await using var file = File.OpenRead(path);
        _ = await file.ReadAsync(header);
        return System.Text.Encoding.ASCII.GetString(header);
    }

    /// <summary>Tells whether a file is protected with a password.</summary>
    /// <param name="path">The file.</param>
    /// <returns><see langword="true"/> when it cannot be opened without one.</returns>
    private static bool IsEncrypted(string path)
    {
        try
        {
            using var opened = new HyperPdfEngine().Open(path, null);
            return opened.GetMetadata().IsEncrypted;
        }
        catch (DocumentOpenException ex) when (ex.Error == DocumentOpenError.Password)
        {
            return true;
        }
    }

    /// <summary>Opens a protected test document with its password, and marks some text so it has unsaved changes.</summary>
    /// <param name="test">The test services.</param>
    /// <param name="main">The viewer.</param>
    /// <param name="name">The file name.</param>
    /// <returns>The tab, with unsaved changes.</returns>
    /// <exception cref="InvalidOperationException">The protected copy could not be written.</exception>
    private static async Task<DocumentTabViewModel> OpenEditedProtectedAsync(TestServices test, MainViewModel main, string name)
    {
        var engine = new HyperPdfEngine();
        using (var plain = engine.Open(test.CreateDocument($"plain-{name}", 1), null))
        {
            await using var output = new MemoryStream();
            var request = new ProtectionRequest { Format = ProtectionFormat.Strong, UserPassword = OwnerPassword, OwnerPassword = OwnerPassword, Permissions = ProtectionPermissions.All };
            var written = await DocumentFeature<IDocumentProtector>.Get(plain)!.ProtectAsync(request, output, CancellationToken.None);
            var path = test.CreateDocument(name, output.ToArray());
            main.Open([path]);
            _ = written.Written ? 0 : throw new InvalidOperationException(written.Refusal);
        }

        var tab = main.SelectedTab!;
        await Assert.That(await UiWait.UntilAsync(() => tab.NeedsPassword)).IsTrue();
        tab.PasswordEntry = OwnerPassword;
        _ = await tab.SubmitPasswordCommand.Execute().ToTask();
        await Assert.That(await UiWait.UntilAsync(() => tab.IsLoaded)).IsTrue();
        var lines = new List<PageRect>();
        tab.TryGetDocument()!.GetTextBounds(0, 0, TestPdf.Sentence.Length, lines);
        await Assert.That(tab.Annotations.MarkText(AnnotationKind.Highlight, new Dictionary<int, List<PageRect>> { [0] = lines }, AnnotationColors.Sage)).IsTrue();
        return tab;
    }
}
