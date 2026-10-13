// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.App.ViewModels;
using HyperPdf.Core.Protection;
using HyperPdf.TestAssets;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Tests;

/// <summary>Checks the protect and remove protection windows' state: the checks before saving, the request, refusals and cleared passwords.</summary>
public sealed class ProtectViewModelTests
{
    /// <summary>The pages of the generated documents.</summary>
    private const int Pages = 2;

    /// <summary>A reading password for the tests.</summary>
    private const string Reading = "reader-pass";

    /// <summary>An owner password for the tests.</summary>
    private const string Owner = "owner-pass";

    /// <summary>The name of the open document.</summary>
    private const string SourceName = "letter.pdf";

    /// <summary>A file name for the copies.</summary>
    private const string CopyName = "kept.pdf";

    /// <summary>The window names the copy after the open file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SuggestsNamesAndAllowsEverythingByDefault()
    {
        using var test = new TestServices();
        using var vm = Create(test, new());

        await Assert.That(vm.SuggestedName).IsEqualTo("letter (protected).pdf");
        await Assert.That(vm.AllowAccessibility).IsTrue();
        await Assert.That(vm.AllowPrint && vm.AllowCopy && vm.AllowEdit && vm.AllowForms).IsTrue();
        await Assert.That(vm.AccessibilityWarning).IsEmpty();
        await Assert.That(vm.NeedsCurrentOwner).IsFalse();
    }

    /// <summary>No password and no limits is refused before asking where to save.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NothingToProtectIsRefused()
    {
        using var test = new TestServices();
        var fake = new FakeDocumentProtector();
        using var vm = Create(test, fake);
        var asked = false;
        using var handler = vm.SaveInteraction.RegisterHandler(context =>
        {
            asked = true;
            context.SetOutput(null);
        });

        _ = await vm.SaveCommand.Execute().ToTask();

        await Assert.That(vm.Error).Contains("Type a reading password or an owner password");
        await Assert.That(asked).IsFalse();
        await Assert.That(fake.Calls).IsEqualTo(0);
    }

    /// <summary>Different repeats and a restriction without an owner password are refused in plain words.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MistakesAreExplained()
    {
        using var test = new TestServices();
        var fake = new FakeDocumentProtector();
        using var vm = Create(test, fake);
        vm.UserPassword = Reading;
        vm.UserPasswordRepeat = "typo";

        _ = await vm.SaveCommand.Execute().ToTask();
        await Assert.That(vm.Error).Contains("reading passwords are not the same");

        vm.UserPasswordRepeat = Reading;
        vm.AllowCopy = false;
        _ = await vm.SaveCommand.Execute().ToTask();
        await Assert.That(vm.Error).Contains("add an owner password that differs");
        await Assert.That(fake.Calls).IsEqualTo(0);
    }

    /// <summary>Clearing the accessibility permission warns, and the warning depends on the encryption.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ClearingAccessibilityWarns()
    {
        using var test = new TestServices();
        using var vm = Create(test, new());

        vm.AllowAccessibility = false;
        await Assert.That(vm.AccessibilityWarning).Contains("no longer lets a file block screen readers");

        vm.SelectedFormat = vm.Formats[1];
        await Assert.That(vm.AccessibilityWarning).StartsWith("Warning: people who use a screen reader will not be able to read this file");

        vm.AllowAccessibility = true;
        await Assert.That(vm.AccessibilityWarning).IsEmpty();
    }

    /// <summary>The request carries the passwords, permissions and encryption; the file is written and no temporary file is left.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavesTheCopyWithTheChosenRequest()
    {
        using var test = new TestServices();
        var fake = new FakeDocumentProtector();
        using var vm = Create(test, fake);
        vm.UserPassword = Reading;
        vm.UserPasswordRepeat = Reading;
        vm.OwnerPassword = Owner;
        vm.OwnerPasswordRepeat = Owner;
        vm.AllowCopy = false;
        vm.AllowAccessibility = false;
        vm.SelectedFormat = vm.Formats[1];
        var destination = Path.Combine(test.Directory, CopyName);
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));

        _ = await vm.SaveCommand.Execute().ToTask();

        var request = fake.LastRequest!;
        using (Assert.Multiple())
        {
            await Assert.That(File.Exists(destination)).IsTrue();
            await Assert.That(File.Exists($"{destination}.protect")).IsFalse();
            await Assert.That(request.Format).IsEqualTo(ProtectionFormat.Compatible);
            await Assert.That(request.UserPassword).IsEqualTo(Reading);
            await Assert.That(request.OwnerPassword).IsEqualTo(Owner);
            await Assert.That(request.Permissions).IsEqualTo(ProtectionPermissions.Print | ProtectionPermissions.Edit | ProtectionPermissions.Forms);
            await Assert.That(request.ToString()).DoesNotContain(Reading);
            await Assert.That(vm.StatusText).IsEqualTo("Done. The copy is saved.");
            await Assert.That(vm.ResultText).Contains("• Screen readers can still read this file.");
        }
    }

    /// <summary>A refusal from the engine saves nothing and shows the reason.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusalSavesNothingAndExplains()
    {
        using var test = new TestServices();
        var fake = new FakeDocumentProtector { Refusal = "This document is signed." };
        using var vm = Create(test, fake);
        vm.UserPassword = Reading;
        vm.UserPasswordRepeat = Reading;
        var destination = Path.Combine(test.Directory, "refused.pdf");
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));

        _ = await vm.SaveCommand.Execute().ToTask();

        await Assert.That(File.Exists(destination)).IsFalse();
        await Assert.That(File.Exists($"{destination}.protect")).IsFalse();
        await Assert.That(vm.ResultText).Contains("• This document is signed.");
        await Assert.That(vm.SavedPath).IsNull();
    }

    /// <summary>A document opened with only its reading password asks for the current owner password and passes it on.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChangingProtectionNeedsTheCurrentOwnerPassword()
    {
        using var test = new TestServices();
        var fake = new FakeDocumentProtector { Status = new(true, false, false) };
        using var vm = Create(test, fake);
        vm.OwnerPassword = Owner;
        vm.OwnerPasswordRepeat = Owner;
        vm.CurrentOwnerPassword = "old-owner";
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(Path.Combine(test.Directory, "changed.pdf")));

        _ = await vm.SaveCommand.Execute().ToTask();

        await Assert.That(vm.NeedsCurrentOwner).IsTrue();
        await Assert.That(fake.LastRequest!.CurrentOwnerPassword).IsEqualTo("old-owner");
    }

    /// <summary>Removing protection passes the owner password on, and a closed window forgets every password.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovalUsesTheOwnerPasswordAndPasswordsAreClearedOnClose()
    {
        using var test = new TestServices();
        var fake = new FakeDocumentProtector { Status = new(true, false, false) };
        var remove = new RemoveProtectionViewModel(fake, test.CreateDocument(SourceName, TestPdf.Create(Pages)));
        remove.OwnerPassword = Owner;
        var destination = Path.Combine(test.Directory, "plain.pdf");
        using var handler = remove.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));

        _ = await remove.SaveCommand.Execute().ToTask();
        await Assert.That(fake.LastOwnerPassword).IsEqualTo(Owner);
        await Assert.That(File.Exists(destination)).IsTrue();
        await Assert.That(remove.NeedsOwnerPassword).IsTrue();
        await Assert.That(remove.SuggestedName).IsEqualTo("letter (unprotected).pdf");

        remove.Dispose();
        await Assert.That(remove.OwnerPassword).IsEmpty();

        var protect = Create(test, new());
        protect.UserPassword = Reading;
        protect.CurrentOwnerPassword = Owner;
        protect.Dispose();
        await Assert.That(protect.UserPassword).IsEmpty();
        await Assert.That(protect.CurrentOwnerPassword).IsEmpty();
    }

    /// <summary>Saving over the open file is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SavingOverTheOpenFileIsRefused()
    {
        using var test = new TestServices();
        var path = test.CreateDocument(SourceName, TestPdf.Create(Pages));
        var fake = new FakeDocumentProtector();
        using var vm = new ProtectViewModel(fake, path);
        vm.UserPassword = Reading;
        vm.UserPasswordRepeat = Reading;
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(path));

        _ = await vm.SaveCommand.Execute().ToTask();

        await Assert.That(vm.Error).Contains("replace the file you have open");
        await Assert.That(fake.Calls).IsEqualTo(0);
    }

    /// <summary>Makes the view model over a generated document.</summary>
    /// <param name="test">The test services.</param>
    /// <param name="fake">The protector.</param>
    /// <returns>The view model.</returns>
    private static ProtectViewModel Create(TestServices test, FakeDocumentProtector fake) =>
        new(fake, test.CreateDocument(SourceName, TestPdf.Create(Pages)));
}
