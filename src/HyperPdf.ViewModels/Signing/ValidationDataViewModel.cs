// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Signatures.Signing;
using HyperPdf.Http.Signatures;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Signing;

/// <summary>
/// The "Add Validation Data or Timestamp" window: stores what each signature needs to be checked years later, or stamps
/// the whole file with a trusted time. It asks where to save, runs with progress that can be cancelled, and shows every
/// note the signer gives. The open file is never replaced.
/// </summary>
[DebuggerDisplay("ValidationDataViewModel: {FileName}")]
public sealed partial class ValidationDataViewModel : ReactiveObject, ICopyWindowRequest, ILocalisationChildren
{
    /// <summary>The bar's value once a copy is saved.</summary>
    private const double FullBar = 100;

    /// <summary>The signer the open document provides.</summary>
    private readonly IDocumentSigner _signer;

    /// <summary>The path of the open document, which must never be overwritten.</summary>
    private readonly string _sourcePath;

    /// <summary>Finds revocation data and missing issuer certificates.</summary>
    private readonly ISignatureRevocationSource _revocation;

    /// <summary>The timestamp authority, or <see langword="null"/> when none is set.</summary>
    private readonly ISignatureTimestamper? _timestamper;

    /// <summary>Owns the run in progress, and the clients this window made, until the window closes.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>Whether a run is not in progress, so a copy can start.</summary>
    private readonly IObservable<bool> _canSave;

    /// <summary>Whether a run is in progress, so it can be cancelled.</summary>
    private readonly IObservable<bool> _canCancel;

    /// <summary>The completed report and destination retained for language changes.</summary>
    private (SigningOutcome Report, string Path)? _resultDescription;

    /// <summary>The resource identity of the displayed StatusText.</summary>
    private LocalizedMessage _localizedStatusText = LocalizedMessage.Get(UiText, "Ready. Choose what to add, then Save Copy.");

    /// <summary>The resource identity of the displayed Error.</summary>
    private LocalizedMessage _localizedError;

    /// <summary>Initializes a new instance of the <see cref="ValidationDataViewModel"/> class that looks answers up over HTTP.</summary>
    /// <param name="signer">The open document's signer.</param>
    /// <param name="sourcePath">The path of the open document.</param>
    /// <param name="timestamper">The timestamp authority from Preferences, or <see langword="null"/> when none is set.</param>
    public ValidationDataViewModel(IDocumentSigner signer, string sourcePath, ISignatureTimestamper? timestamper)
        : this(signer, sourcePath, new RevocationSourceClient(), timestamper, true)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ValidationDataViewModel"/> class.</summary>
    /// <param name="signer">The open document's signer.</param>
    /// <param name="sourcePath">The path of the open document.</param>
    /// <param name="revocation">Finds revocation data and missing issuer certificates.</param>
    /// <param name="timestamper">The timestamp authority, or <see langword="null"/> when none is set.</param>
    public ValidationDataViewModel(IDocumentSigner signer, string sourcePath, ISignatureRevocationSource revocation, ISignatureTimestamper? timestamper)
        : this(signer, sourcePath, revocation, timestamper, false)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="ValidationDataViewModel"/> class.</summary>
    /// <param name="signer">The open document's signer.</param>
    /// <param name="sourcePath">The path of the open document.</param>
    /// <param name="revocation">Finds revocation data and missing issuer certificates.</param>
    /// <param name="timestamper">The timestamp authority, or <see langword="null"/> when none is set.</param>
    /// <param name="ownsRevocation">Whether this window disposes <paramref name="revocation"/>.</param>
    private ValidationDataViewModel(IDocumentSigner signer, string sourcePath, ISignatureRevocationSource revocation, ISignatureTimestamper? timestamper, bool ownsRevocation)
    {
        ArgumentNullException.ThrowIfNull(signer);
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        ArgumentNullException.ThrowIfNull(revocation);
        _signer = signer;
        _sourcePath = sourcePath;
        _revocation = revocation;
        _timestamper = timestamper;
        if (ownsRevocation && revocation is IDisposable owned)
        {
            _subscriptions.Add(owned);
        }

        FileName = Path.GetFileName(sourcePath);
        SelectedAction = Actions[0];
        _canSave = this.WhenChanged(static vm => vm.IsRunning).Select(static running => !running);
        _canCancel = this.WhenChanged(static vm => vm.IsRunning);
        _subscriptions.Add(this.WhenChanged(static vm => vm.Run).OwnLatest().SubscribeSafeErrors(ErrorTrace.Write));
    }

    /// <summary>Gets the choices: validation data first, then a timestamp.</summary>
    public IReadOnlyList<ValidationActionChoice> Actions { get; } =
    [

        new(
            ValidationAction.AddValidationData,
            "Add validation data",
            "Stores the certificates and the revocation answers (OCSP and CRL) each signature needs, so it can still be checked after the "
                + "certificates expire. This looks them up on the internet."),
        new(
            ValidationAction.AddTimestamp,
            "Add a document timestamp",
            "Stamps the whole file with a trusted time from the timestamp server in Preferences, so you can show later that it existed now. Add "
                + "validation data first, so the stamp covers it.")
    ];

    /// <summary>Gets the name of the open document.</summary>
    public string FileName { get; }

    /// <summary>Gets the interaction asking where to save the copy; the input is the suggested file name.</summary>
    public Interaction<string, string?> SaveInteraction { get; } = new();

    /// <summary>Gets or sets the chosen action.</summary>
    [Reactive]
    public partial ValidationActionChoice? SelectedAction { get; set; }

    /// <summary>Gets or sets a value indicating whether each signature gets its own record in the validation data.</summary>
    [Reactive]
    public partial bool IncludeVri { get; set; } = true;

    /// <summary>Gets a value indicating whether a copy is being written.</summary>
    [Reactive]
    public partial bool IsRunning { get; private set; }

    /// <summary>Gets how full the bar is, from 0 to 100: full once a copy is saved, empty otherwise.</summary>
    [Reactive]
    public partial double ProgressPercent { get; private set; }

    /// <summary>Gets what the run is doing now, or how it ended.</summary>
    public string StatusText { get => _localizedStatusText.Value ?? string.Empty; private set => SetStatusText(LocalizedMessage.Literal(value)); }

    /// <summary>Gets what went wrong, or <see langword="null"/>.</summary>
    public string? Error { get => _localizedError.Value; private set => SetError(LocalizedMessage.Literal(value)); }

    /// <summary>Gets the plain-words summary of the last finished run; empty before one finishes.</summary>
    [Reactive]
    public partial string ResultText { get; private set; } = string.Empty;

    /// <summary>Gets the path of the last saved copy, or <see langword="null"/>.</summary>
    [Reactive]
    public partial string? SavedPath { get; private set; }

    /// <summary>Gets the action the window currently describes.</summary>
    public ValidationAction Action => SelectedAction?.Action ?? ValidationAction.AddValidationData;

    /// <summary>Gets the file name suggested for the copy.</summary>
    public string SuggestedName => Action == ValidationAction.AddTimestamp
        ? $"{Path.GetFileNameWithoutExtension(FileName)}-timestamped.pdf"
        : $"{Path.GetFileNameWithoutExtension(FileName)}-validation.pdf";

    /// <summary>Gets or sets the run in progress, which disposing cancels, or <see langword="null"/> when none is.</summary>
    [Reactive]
    private partial CancellationDisposable? Run { get; set; }

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <summary>Sets the displayed StatusText while retaining its resource identity.</summary>
    /// <param name="message">The message.</param>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void SetStatusText(LocalizedMessage message) => this.RaiseAndSetIfChanged(ref _localizedStatusText, message, nameof(StatusText));

    /// <summary>Sets the displayed Error while retaining its resource identity.</summary>
    /// <param name="message">The message.</param>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void SetError(LocalizedMessage message) => this.RaiseAndSetIfChanged(ref _localizedError, message, nameof(Error));

    /// <inheritdoc/>
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        RefreshResultText();
        this.RaisePropertyChanged(nameof(StatusText));
        this.RaisePropertyChanged(nameof(Error));
        LocalisationRefresh.RefreshCore(Actions, visited);
    }

    /// <summary>Stops the copy in progress.</summary>
    [ReactiveCommand(CanExecute = nameof(_canCancel))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Cancel() => Run = null;

    /// <summary>Stops any copy in progress and asks the window to close.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Close() => Cancel();

    /// <summary>Asks where to save, then writes the updated copy there.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canSave))]
    private async Task SaveAsync()
    {
        SetError(LocalizedMessage.Literal(null));
        if (Action == ValidationAction.AddTimestamp && _timestamper is null)
        {
            SetError(LocalizedMessage.Get(UiText, "No timestamp server is set. Type its address in Preferences, then try again."));
            return;
        }

        var path = await SaveInteraction.Handle(SuggestedName).ToTask().ConfigureAwait(true);
        if (string.IsNullOrEmpty(path))
        {
            return;
        }

        if (SignedCopyFile.IsSameFile(path, _sourcePath))
        {
            SetError(LocalizedMessage.Get(UiText, "That would replace the file you have open. Choose a different name for the copy."));
            return;
        }

        var run = new CancellationDisposable();
        Run = run;
        IsRunning = true;
        ProgressPercent = 0;
        _resultDescription = null;
        ResultText = string.Empty;
        SetStatusText(
            Action == ValidationAction.AddTimestamp
                ? LocalizedMessage.Get(UiText, "Asking the timestamp server for a trusted time.")
                : LocalizedMessage.Get(UiText, "Looking up the certificates and revocation answers. This can take a while."));
        try
        {
            var outcome = await RunAsync(run.Token).ConfigureAwait(true);
            await SignedCopyFile.WriteAsync(outcome, path, run.Token).ConfigureAwait(true);
            SavedPath = path;
            ProgressPercent = FullBar;
            SetStatusText(LocalizedMessage.Get(UiText, "Done. The copy is saved."));
            _resultDescription = (outcome, path);
            RefreshResultText();
        }
        catch (OperationCanceledException)
        {
            SetStatusText(LocalizedMessage.Get(UiText, "Cancelled. No file was saved."));
        }
        catch (Exception ex) when (CopyFailure.IsExpected(ex) || ex is HttpRequestException)
        {
            SetError(LocalizedMessage.Format(UiText, "Could not save the copy: {0}", CopyFailure.Message(ex)));
            SetStatusText(LocalizedMessage.Get(UiText, "The copy was not saved."));
        }
        finally
        {
            if (ReferenceEquals(Run, run))
            {
                Run = null;
            }

            IsRunning = false;
        }
    }

    /// <summary>Adds the validation data or the timestamp, off the UI thread.</summary>
    /// <param name="cancellationToken">Stops the run.</param>
    /// <returns>The updated file and its notes.</returns>
    private Task<SigningOutcome> RunAsync(CancellationToken cancellationToken)
    {
        var action = Action;
        var includeVri = IncludeVri;

        // Building the update is CPU work, so it starts on the pool; the lookups then await. The captures are the choices
        // made when the run started, so a change in the window cannot alter a run in progress.
        return action == ValidationAction.AddTimestamp
            ? Task.Run(
            () => _signer.AddDocumentTimestampAsync(
                _timestamper!,
                SigningDigest.Sha256,
                cancellationToken).AsTask(),
            cancellationToken)
            : Task.Run(() => _signer.AddValidationDataAsync(_revocation, includeVri, cancellationToken).AsTask(), cancellationToken);
    }

    /// <summary>Rewords the retained report without repeating the operation.</summary>
    private void RefreshResultText()
    {
        if (_resultDescription is not { } description)
        {
            return;
        }

        var(outcome, path) = description;
        ResultText = OptimizeReportText.Join(SignedCopyFile.Lines(outcome, path));
    }
}
