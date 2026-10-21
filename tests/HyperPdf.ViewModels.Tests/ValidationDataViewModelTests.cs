// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Signatures.Signing;
using HyperPdf.TestAssets;
using HyperPdf.ViewModels;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Tests for <see cref="ValidationDataViewModel"/> and the tab command that opens it.</summary>
public sealed class ValidationDataViewModelTests
{
    /// <summary>The pages of the generated documents.</summary>
    private const int Pages = 2;

    /// <summary>The signatures in a file that was signed and then timestamped.</summary>
    private const int SignedAndStamped = 2;

    /// <summary>The document name.</summary>
    private const string SourceName = "contract.pdf";

    /// <summary>The ending of the temporary file a copy is written to.</summary>
    private const string TemporarySuffix = ".signing";

    /// <summary>The longest wait for a run to start.</summary>
    private static readonly TimeSpan StartTimeout = TimeSpan.FromSeconds(30);

    /// <summary>The time the test timestamp authority stamps with.</summary>
    private static readonly DateTimeOffset StampTime = new(2026, 1, 2, 3, 4, 5, TimeSpan.Zero);

    /// <summary>The window offers validation data first, then a timestamp, with plain descriptions and a name for each copy.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OffersValidationDataThenATimestamp()
    {
        using var test = new TestServices();
        using var vm = Create(test, new(), false);

        using (Assert.Multiple())
        {
            await Assert.That(vm.Actions.Select(static a => a.Action)).IsEquivalentTo([ValidationAction.AddValidationData, ValidationAction.AddTimestamp]);
            await Assert.That(vm.Action).IsEqualTo(ValidationAction.AddValidationData);
            await Assert.That(vm.IncludeVri).IsTrue();
            await Assert.That(vm.SuggestedName).IsEqualTo("contract-validation.pdf");

            vm.SelectedAction = vm.Actions[1];

            await Assert.That(vm.SuggestedName).IsEqualTo("contract-timestamped.pdf");
        }
    }

    /// <summary>Validation data is asked of the signer with the chosen record option, saved without a temporary file, and its notes are shown.</summary>
    /// <param name="includeVri">Whether each signature gets its own record.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(true)]
    [Arguments(false)]
    public async Task ValidationDataIsSavedAndNotesAreShown(bool includeVri)
    {
        using var test = new TestServices();
        var fake = new FakeDocumentSigner { Notes = ["No revocation answer was found for the signer's certificate."] };
        using var vm = Create(test, fake, false);
        vm.IncludeVri = includeVri;
        var destination = Path.Combine(test.Directory, "validated.pdf");
        string? suggested = null;
        using var handler = vm.SaveInteraction.RegisterHandler(context =>
        {
            suggested = context.Input;
            context.SetOutput(destination);
        });

        _ = await vm.SaveCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(suggested).IsEqualTo("contract-validation.pdf");
            await Assert.That(fake.Calls).IsEquivalentTo([nameof(IDocumentSigner.AddValidationDataAsync)]);
            await Assert.That(fake.IncludedVri).IsEqualTo(includeVri);
            await Assert.That(File.Exists(destination)).IsTrue();
            await Assert.That(File.Exists(destination + TemporarySuffix)).IsFalse();
            await Assert.That(vm.SavedPath).IsEqualTo(destination);
            await Assert.That(vm.StatusText).IsEqualTo("Done. The copy is saved.");
            await Assert.That(vm.ResultText).Contains("Saved as validated.pdf.");
            await Assert.That(vm.ResultText).Contains("• No revocation answer was found for the signer's certificate.");
            await Assert.That(vm.Error).IsNull();
        }
    }

    /// <summary>A timestamp is asked of the signer and saved.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TimestampIsAddedWhenAServerIsSet()
    {
        using var test = new TestServices();
        var fake = new FakeDocumentSigner();
        using var vm = Create(test, fake, true);
        vm.SelectedAction = vm.Actions[1];
        var destination = Path.Combine(test.Directory, "stamped.pdf");
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));

        _ = await vm.SaveCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(fake.Calls).IsEquivalentTo([nameof(IDocumentSigner.AddDocumentTimestampAsync)]);
            await Assert.That(File.Exists(destination)).IsTrue();
            await Assert.That(vm.ResultText).Contains("Saved as stamped.pdf.");
        }
    }

    /// <summary>A timestamp without a server is explained before anything is asked of the user.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TimestampWithoutAServerIsExplained()
    {
        using var test = new TestServices();
        var fake = new FakeDocumentSigner();
        using var vm = Create(test, fake, false);
        vm.SelectedAction = vm.Actions[1];
        var asked = false;
        using var handler = vm.SaveInteraction.RegisterHandler(context =>
        {
            asked = true;
            context.SetOutput(Path.Combine(test.Directory, "never.pdf"));
        });

        _ = await vm.SaveCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(vm.Error).IsEqualTo("No timestamp server is set. Type its address in Preferences, then try again.");
            await Assert.That(asked).IsFalse();
            await Assert.That(fake.Calls).IsEmpty();
        }
    }

    /// <summary>A destination that is the open file is refused, and nothing is written.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NeverOverwritesTheOpenFile()
    {
        using var test = new TestServices();
        var fake = new FakeDocumentSigner();
        using var vm = Create(test, fake, false);
        var source = Path.Combine(test.Directory, SourceName);
        var before = await File.ReadAllBytesAsync(source);
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(source));

        _ = await vm.SaveCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(fake.Calls).IsEmpty();
            await Assert.That(vm.Error).Contains("replace the file you have open");
            await Assert.That(await File.ReadAllBytesAsync(source)).IsEquivalentTo(before);
        }
    }

    /// <summary>Cancel stops the run and keeps no file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CancelStopsTheRunAndKeepsNoFile()
    {
        using var test = new TestServices();
        var fake = new FakeDocumentSigner { Gate = new(TaskCreationOptions.RunContinuationsAsynchronously) };
        using var vm = Create(test, fake, false);
        var destination = Path.Combine(test.Directory, "cancelled.pdf");
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));

        var run = vm.SaveCommand.Execute().ToTask();
        await fake.Started.Task.WaitAsync(StartTimeout);
        await Assert.That(vm.IsRunning).IsTrue();
        _ = await vm.CancelCommand.Execute().ToTask();
        _ = await run;

        using (Assert.Multiple())
        {
            await Assert.That(vm.IsRunning).IsFalse();
            await Assert.That(vm.StatusText).IsEqualTo("Cancelled. No file was saved.");
            await Assert.That(File.Exists(destination)).IsFalse();
            await Assert.That(File.Exists(destination + TemporarySuffix)).IsFalse();
        }
    }

    /// <summary>A failed run says what went wrong in words and keeps no file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task FailureIsExplained()
    {
        using var test = new TestServices();
        var fake = new FakeDocumentSigner { Failure = new InvalidDataException("The file is damaged.") };
        using var vm = Create(test, fake, false);
        var destination = Path.Combine(test.Directory, "failed.pdf");
        using var handler = vm.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));

        _ = await vm.SaveCommand.Execute().ToTask();

        using (Assert.Multiple())
        {
            await Assert.That(vm.Error).IsEqualTo("Could not save the copy: The file is damaged.");
            await Assert.That(vm.StatusText).IsEqualTo("The copy was not saved.");
            await Assert.That(File.Exists(destination)).IsFalse();
        }
    }

    /// <summary>The tab command opens the window for a document the engine can sign, and a timestamp from an authority signs the real file.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TabCommandStampsARealSignedFile()
    {
        using var test = new TestServices();
        using var authority = new TestTimestampAuthority(StampTime);
        using var certificate = TestSignedPdf.CreateCertificate(TimeProvider.System);
        var signedPath = test.CreateDocument("signed.pdf", TestSignedPdf.Create(Pages, certificate));
        using var main = new MainViewModel(test.Services);
        await TestServices.OpenAndWaitAsync(main, [signedPath]);
        var tab = main.SelectedTab!;
        ValidationDataViewModel? request = null;
        using var window = tab.Copies.Validation.Interaction.RegisterHandler(context =>
        {
            request = context.Input;
            context.SetOutput(RxVoid.Default);
        });

        _ = await tab.Copies.Validation.RunCommand.Execute().ToTask();
        var signer = DocumentFeature<IDocumentSigner>.Get(tab.TryGetDocument())!;
        using var stamping = new ValidationDataViewModel(signer, signedPath, new NoRevocationSource(), authority);
        stamping.SelectedAction = stamping.Actions[1];
        var destination = Path.Combine(test.Directory, "stamped.pdf");
        using var picker = stamping.SaveInteraction.RegisterHandler(context => context.SetOutput(destination));
        _ = await stamping.SaveCommand.Execute().ToTask();
        main.Open([destination]);
        var stamped = main.Tabs.First(t => t.FilePath == destination);
        await stamped.EnsureLoadedAsync(CancellationToken.None);
        stamped.Signatures.Refresh();

        using (Assert.Multiple())
        {
            await Assert.That(tab.Copies.Validation.CanRun).IsTrue();
            await Assert.That(request).IsNotNull();
            await Assert.That(request!.FileName).IsEqualTo("signed.pdf");
            await Assert.That(stamping.Error).IsNull();
            await Assert.That(stamped.Signatures.SignatureCount).IsEqualTo(SignedAndStamped);
        }
    }

    /// <summary>The notes after a run are written as a message and as separate points.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NotesAreWrittenForTheNoticeAndTheSummary()
    {
        var withNotes = new SigningOutcome([], ["The PDF version was raised to 2.0.", "No timestamp was added."]);
        var without = new SigningOutcome([], []);

        using (Assert.Multiple())
        {
            await Assert.That(SignedCopyFile.NotesSuffix(withNotes)).IsEqualTo(" The PDF version was raised to 2.0. No timestamp was added.");
            await Assert.That(SignedCopyFile.NotesSuffix(without)).IsEmpty();
            await Assert.That(SignedCopyFile.Lines(withNotes, "/tmp/a/signed.pdf")).IsEquivalentTo(["Saved as signed.pdf.", "• The PDF version was raised to 2.0.", "• No timestamp was added."]);
        }
    }

    /// <summary>Makes the window's view model over a generated document.</summary>
    /// <param name="test">The test services.</param>
    /// <param name="signer">The signer.</param>
    /// <param name="withServer">Whether a timestamp authority is set.</param>
    /// <returns>The view model.</returns>
    private static ValidationDataViewModel Create(TestServices test, FakeDocumentSigner signer, bool withServer) =>
        new(signer, test.CreateDocument(SourceName, Pages), new NoRevocationSource(), withServer ? new FixedTimestamper() : null);

    /// <summary>A timestamp authority that is never asked, because the fake signer ignores it.</summary>
    private sealed class FixedTimestamper : ISignatureTimestamper
    {
        /// <inheritdoc/>
        public int EstimatedTokenSize => 0;

        /// <inheritdoc/>
        public ValueTask<byte[]> TimestampAsync(ReadOnlyMemory<byte> hash, SigningDigest digest, CancellationToken cancellationToken) => throw new NotSupportedException("The fake signer never asks.");
    }
}
