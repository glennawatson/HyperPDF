// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdf.ViewModels;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Tests the protect and remove protection commands of a tab against the real engine.</summary>
public sealed class ProtectCopyTests
{
    /// <summary>The pages of the generated document.</summary>
    private const int Pages = 2;

    /// <summary>A reading password.</summary>
    private const string Reading = "open sesame";

    /// <summary>An owner password.</summary>
    private const string Owner = "master key";

    /// <summary>The protect command is on for a plain document and the remove command says why it is off; a real copy is encrypted and holds neither password.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ProtectsARealCopyAndExplainsWhyRemovalIsOff()
    {
        using var test = new TestServices();
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [test.CreateDocument("plain.pdf", Pages)]);
        var tab = main.SelectedTab!;
        var destination = Path.Combine(test.Directory, "protected.pdf");
        string? status = null;
        string? error = null;
        using var handler = tab.Copies.Protect.Interaction.RegisterHandler(async context =>
        {
            var window = context.Input;
            window.UserPassword = Reading;
            window.UserPasswordRepeat = Reading;
            window.OwnerPassword = Owner;
            window.OwnerPasswordRepeat = Owner;
            using var picker = window.SaveInteraction.RegisterHandler(pick => pick.SetOutput(destination));
            _ = await window.SaveCommand.Execute().ToTask();
            status = window.StatusText;
            error = window.Error;
            context.SetOutput(RxVoid.Default);
        });

        _ = await tab.Copies.Protect.RunCommand.Execute().ToTask();
        var saved = await File.ReadAllBytesAsync(destination);
        var text = Encoding.Latin1.GetString(saved);

        using (Assert.Multiple())
        {
            await Assert.That(tab.Copies.Protect.CanRun).IsTrue();
            await Assert.That(tab.Copies.Protect.MenuText).IsEqualTo("Protect with _Password…");
            await Assert.That(tab.Copies.Unprotect.CanRun).IsFalse();
            await Assert.That(tab.Copies.Unprotect.MenuText).Contains("needs a protected document");
            await Assert.That(error).IsNull();
            await Assert.That(status).IsEqualTo("Done. The copy is saved.");
            await Assert.That(text).Contains("/Encrypt");
            await Assert.That(text).DoesNotContain(Reading);
            await Assert.That(text).DoesNotContain(Owner);
        }
    }
}
