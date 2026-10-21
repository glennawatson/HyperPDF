// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using System.Security.Cryptography;
using HyperPdf.Core.Signatures.Signing;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Signing;

/// <summary>
/// The "Sign with External Signer" window, for a key that stays on a hardware token or with a signing service. The user
/// names the signer's certificates, the window prepares the file and saves the bytes to sign, the user has their signer
/// sign them, and the window finishes the copy from the signer's answer. The private key never reaches the app.
/// </summary>
[DebuggerDisplay("ExternalSignViewModel: {FileName}")]
public sealed partial class ExternalSignViewModel : ReactiveObject, ICopyWindowRequest
{
    /// <summary>Why a chosen destination was refused.</summary>
    private const string OverwriteMessage = "That would replace the file you have open. Choose a different name for the copy.";

    /// <summary>The status shown before anything has happened.</summary>
    private const string ReadyStatus = "Ready. Choose the signer's certificate files, then Prepare.";

    /// <summary>The bar's value once a copy is saved.</summary>
    private const double FullBar = 100;

    /// <summary>The subscriptions this window's state owns.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>Opens a saved copy in the viewer, or <see langword="null"/> to leave it closed.</summary>
    private readonly Action<string>? _openCopy;

    /// <summary>The signer the open document provides.</summary>
    private readonly IDocumentSigner _signer;

    /// <summary>The path of the open document, which must never be overwritten.</summary>
    private readonly string _sourcePath;

    /// <summary>The page a new signature field goes on.</summary>
    private readonly int _pageIndex;

    /// <summary>The timestamp authority, or <see langword="null"/> when none is set.</summary>
    private readonly ISignatureTimestamper? _timestamper;

    /// <summary>Whether certificates are chosen and no run is in progress, so the file can be prepared.</summary>
    private readonly IObservable<bool> _canPrepare;

    /// <summary>Whether a signature is prepared and no run is in progress, so it can be saved or finished.</summary>
    private readonly IObservable<bool> _canFinish;

    /// <summary>Whether a run is in progress, so it can be cancelled.</summary>
    private readonly IObservable<bool> _canCancel;

    /// <summary>The signer's certificates, once chosen.</summary>
    private LoadedSigner? _loaded;

    /// <summary>The prepared signature that waits for the signer's answer.</summary>
    private IPendingSignature? _pending;

    /// <summary>Stops the run in progress; <see langword="null"/> when none is.</summary>
    private CancellationTokenSource? _cancel;

    /// <summary>Initializes a new instance of the <see cref="ExternalSignViewModel"/> class.</summary>
    /// <param name="signer">The open document's signer.</param>
    /// <param name="sourcePath">The path of the open document.</param>
    /// <param name="pageIndex">The zero-based page a new signature field goes on.</param>
    /// <param name="timestamper">The timestamp authority, or <see langword="null"/> for none.</param>
    /// <param name="openCopy">Opens a saved copy in the viewer, or <see langword="null"/> to leave it closed.</param>
    public ExternalSignViewModel(IDocumentSigner signer, string sourcePath, int pageIndex, ISignatureTimestamper? timestamper, Action<string>? openCopy)
    {
        ArgumentNullException.ThrowIfNull(signer);
        ArgumentException.ThrowIfNullOrEmpty(sourcePath);
        _signer = signer;
        _sourcePath = sourcePath;
        _pageIndex = pageIndex;
        _timestamper = timestamper;
        _openCopy = openCopy;
        FileName = Path.GetFileName(sourcePath);
        SelectedKey = KeyChoices[0];
        SelectedResult = ResultChoices[0];
        _canPrepare = this.WhenChanged(static vm => vm.HasSigner, static vm => vm.IsRunning, static (has, running) => has && !running);
        _canFinish = this.WhenChanged(static vm => vm.HasPending, static vm => vm.IsRunning, static (has, running) => has && !running);
        _canCancel = this.WhenChanged(static vm => vm.IsRunning);

        // A prepared file belongs to the choices it was made with, so changing one discards it.
        _subscriptions.Add(this.WhenChanged(static vm => vm.Reason, static vm => vm.Location, static vm => vm.SelectedKey, static (_, _, _) => RxVoid.Default)
            .Skip(1)
            .SubscribeSafe(_ => Discard(), ErrorTrace.Write));

        // Closing the window stops a run in progress; the run itself disposes its source when it ends.
        _subscriptions.Add(Scope.Create(Cancel));
    }

    /// <summary>Gets the kinds of signature the signer's key can make.</summary>
    public IReadOnlyList<SigningKeyChoice> KeyChoices { get; } =
    [
        new(SigningKeyKind.RsaPkcs1, "RSA (the usual kind)"),
        new(SigningKeyKind.RsaPss, "RSA with PSS padding"),
        new(SigningKeyKind.Ecdsa, "Elliptic curve (ECDSA)"),
    ];

    /// <summary>Gets what the signer can give back.</summary>
    public IReadOnlyList<ExternalResultChoice> ResultChoices { get; } =
    [
        new(ExternalResultKind.SignatureValue, "The signature value (raw bytes) over the bytes to sign"),
        new(ExternalResultKind.Container, "A whole signature container (CMS, DER) made over the document digest"),
    ];

    /// <summary>Gets the name of the open document.</summary>
    public string FileName { get; }

    /// <summary>Gets the interaction asking for the signer's certificate files; the output is the chosen files or <see langword="null"/>.</summary>
    public Interaction<RxVoid, string[]?> BrowseInteraction { get; } = new();

    /// <summary>Gets the interaction asking where to save the bytes to sign; the input is the suggested file name.</summary>
    public Interaction<string, string?> SaveToSignInteraction { get; } = new();

    /// <summary>Gets the interaction asking for the file the signer gave back.</summary>
    public Interaction<RxVoid, string?> OpenSignatureInteraction { get; } = new();

    /// <summary>Gets the interaction asking where to save the signed copy; the input is the suggested file name.</summary>
    public Interaction<string, string?> SaveCopyInteraction { get; } = new();

    /// <summary>Gets a value indicating whether the signer's certificates are loaded.</summary>
    [Reactive]
    public partial bool HasSigner { get; private set; }

    /// <summary>Gets who the loaded certificates belong to, in plain words; empty before any are loaded.</summary>
    [Reactive]
    public partial string SignerText { get; private set; } = string.Empty;

    /// <summary>Gets or sets the kind of signature the signer's key makes.</summary>
    [Reactive]
    public partial SigningKeyChoice? SelectedKey { get; set; }

    /// <summary>Gets or sets why the document is signed; optional.</summary>
    [Reactive]
    public partial string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets where it is signed; optional.</summary>
    [Reactive]
    public partial string Location { get; set; } = string.Empty;

    /// <summary>Gets a value indicating whether a signature is prepared and waits for the signer.</summary>
    [Reactive]
    public partial bool HasPending { get; private set; }

    /// <summary>Gets what was prepared, in plain words, with the digests a signer needs; empty when nothing is prepared.</summary>
    [Reactive]
    public partial string PendingText { get; private set; } = string.Empty;

    /// <summary>Gets or sets what the signer gives back.</summary>
    [Reactive]
    public partial ExternalResultChoice? SelectedResult { get; set; }

    /// <summary>Gets a value indicating whether a run is in progress.</summary>
    [Reactive]
    public partial bool IsRunning { get; private set; }

    /// <summary>Gets how full the bar is, from 0 to 100: full once a copy is saved, empty otherwise.</summary>
    [Reactive]
    public partial double ProgressPercent { get; private set; }

    /// <summary>Gets what the window is doing now, or how the last step ended.</summary>
    [Reactive]
    public partial string StatusText { get; private set; } = ReadyStatus;

    /// <summary>Gets what went wrong, or <see langword="null"/>.</summary>
    [Reactive]
    public partial string? Error { get; private set; }

    /// <summary>Gets the plain-words summary of the last finished copy; empty before one finishes.</summary>
    [Reactive]
    public partial string ResultText { get; private set; } = string.Empty;

    /// <summary>Gets the path of the last saved copy, or <see langword="null"/>.</summary>
    [Reactive]
    public partial string? SavedPath { get; private set; }

    /// <summary>Gets the kind of answer the window finishes from.</summary>
    public ExternalResultKind ResultKind => SelectedResult?.Kind ?? ExternalResultKind.SignatureValue;

    /// <inheritdoc/>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Dispose() => _subscriptions.Dispose();

    /// <summary>Determines whether an exception is a signing failure to tell the user about, rather than a bug.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns><see langword="true"/> for a damaged file, a refusal, an answer that does not fit, a file error or an unreachable timestamp server.</returns>
    private static bool IsSigningFailure(Exception exception) =>
        exception is InvalidDataException or SigningRefusedException or ArgumentException or NotSupportedException or CryptographicException
            or IOException or UnauthorizedAccessException or HttpRequestException or InvalidOperationException;

    /// <summary>Describes a prepared signature in plain words, with the digest a hash-based signer needs.</summary>
    /// <param name="pending">The prepared signature.</param>
    /// <returns>The description, one point per line.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string DescribePending(IPendingSignature pending) => OptimizeReportText.Join(
    [
        $"Signature field: {pending.FieldName}.",
        $"Signature type: {SigningPreviewText.FormatName(pending.Format)}, digest {SigningPreviewText.DigestName(pending.Digest)}.",
        $"Bytes to sign: {pending.SignedAttributes.Length.ToString(CultureInfo.CurrentCulture)} bytes (DER signed attributes). Your signer hashes them and signs the hash.",
        $"Document digest, if your signing service wants a hash: {Convert.ToHexString(pending.DocumentDigest.Span)}",
    ]);

    /// <summary>Stops the run in progress.</summary>
    [ReactiveCommand(CanExecute = nameof(_canCancel))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Cancel() => Volatile.Read(ref _cancel)?.Cancel();

    /// <summary>Stops any run in progress and asks the window to close.</summary>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Close() => Cancel();

    /// <summary>Asks for the signer's certificate files and reads them.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task BrowseAsync()
    {
        Error = null;
        if (await BrowseInteraction.Handle(RxVoid.Default).ToTask().ConfigureAwait(true) is not { Length: > 0 } paths)
        {
            return;
        }

        try
        {
            var loaded = SignerCertificateFiles.Load(paths);
            _loaded = loaded;

            // The choices follow the order of the key kinds, which start at RSA.
            SelectedKey = KeyChoices[(int)loaded.Identity.KeyKind - (int)SigningKeyKind.RsaPkcs1];
            var count = loaded.Identity.Certificates.Count;
            SignerText = count == 1 ? $"{loaded.Subject}: 1 certificate read." : $"{loaded.Subject}: {count.ToString(CultureInfo.CurrentCulture)} certificates read.";
            HasSigner = true;
            Discard();
            StatusText = "The certificates are read. Choose Prepare to build the file to sign.";
        }
        catch (Exception ex) when (ex is CryptographicException or IOException or UnauthorizedAccessException)
        {
            _loaded = null;
            HasSigner = false;
            SignerText = string.Empty;
            Error = $"Could not read the certificates: {ex.Message}";
        }
    }

    /// <summary>Builds the file with room for the signature and works out the bytes the signer must sign.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canPrepare))]
    private async Task PrepareAsync()
    {
        if (_loaded is not { } loaded)
        {
            return;
        }

        var identity = loaded.Identity with { KeyKind = SelectedKey?.Kind ?? loaded.Identity.KeyKind };
        var request = new SigningRequest(_pageIndex, Reason, Location, TimeProvider.System.GetUtcNow());
        var cancel = Begin("Preparing the file. This can take a while for long files.");
        try
        {
            var pending = await Task.Run(() => _signer.PrepareAsync(identity, request, _timestamper, cancel.Token).AsTask(), cancel.Token).ConfigureAwait(true);
            _pending = pending;
            HasPending = true;
            PendingText = DescribePending(pending);
            StatusText = "Prepared. Save the bytes to sign, have your signer sign them, then choose Finish.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled. Nothing was prepared.";
        }
        catch (Exception ex) when (IsSigningFailure(ex))
        {
            Error = $"Could not prepare the signature: {CopyFailure.Describe(ex)}";
            StatusText = "Nothing was prepared.";
        }
        finally
        {
            End(cancel);
        }
    }

    /// <summary>Saves the bytes the signer must sign.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canFinish))]
    private async Task SaveToSignAsync()
    {
        Error = null;
        if (_pending is not { } pending)
        {
            return;
        }

        var suggested = $"{Path.GetFileNameWithoutExtension(FileName)}-to-sign.bin";
        if (await SaveToSignInteraction.Handle(suggested).ToTask().ConfigureAwait(true) is not { Length: > 0 } path)
        {
            return;
        }

        try
        {
            await File.WriteAllBytesAsync(path, pending.SignedAttributes, CancellationToken.None).ConfigureAwait(true);
            StatusText = $"Saved the bytes to sign as {Path.GetFileName(path)}.";
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException)
        {
            Error = $"Could not save the bytes to sign: {ex.Message}";
        }
    }

    /// <summary>Asks for the signer's answer and for where to save, then finishes the signed copy.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canFinish))]
    private async Task FinishAsync()
    {
        Error = null;
        if (_pending is not { } pending
            || await OpenSignatureInteraction.Handle(RxVoid.Default).ToTask().ConfigureAwait(true) is not { Length: > 0 } answerPath)
        {
            return;
        }

        var suggested = $"{Path.GetFileNameWithoutExtension(FileName)}-signed.pdf";
        if (await SaveCopyInteraction.Handle(suggested).ToTask().ConfigureAwait(true) is not { Length: > 0 } path)
        {
            return;
        }

        if (SignedCopyFile.IsSameFile(path, _sourcePath))
        {
            Error = OverwriteMessage;
            return;
        }

        var cancel = Begin("Finishing the signature.");
        try
        {
            var answer = await File.ReadAllBytesAsync(answerPath, cancel.Token).ConfigureAwait(true);
            var container = ResultKind == ExternalResultKind.Container;
            var token = cancel.Token;

            // Completing the signature builds the whole file before its first await, so it starts on the pool.
            var outcome = await Task.Run(
                () => container
                    ? _signer.CompleteWithContainerAsync(pending, answer, token).AsTask()
                    : _signer.CompleteAsync(pending, answer, token).AsTask(),
                token).ConfigureAwait(true);
            await SignedCopyFile.WriteAsync(outcome, path, cancel.Token).ConfigureAwait(true);
            ShowSaved(outcome, path);
        }
        catch (OperationCanceledException)
        {
            StatusText = "Cancelled. No file was saved.";
        }
        catch (Exception ex) when (IsSigningFailure(ex))
        {
            Error = $"Could not finish the signature: {CopyFailure.Describe(ex)}";
            StatusText = "The copy was not saved.";
        }
        finally
        {
            End(cancel);
        }
    }

    /// <summary>Forgets the prepared signature, because the choices it was made with changed.</summary>
    private void Discard()
    {
        _pending = null;
        HasPending = false;
        PendingText = string.Empty;
    }

    /// <summary>Starts a run: clears the last result and makes the token that stops it.</summary>
    /// <param name="status">What the run is doing.</param>
    /// <returns>The source of the token that stops the run.</returns>
    private CancellationTokenSource Begin(string status)
    {
        Error = null;
        var cancel = new CancellationTokenSource();
        Volatile.Write(ref _cancel, cancel);
        IsRunning = true;
        ProgressPercent = 0;
        ResultText = string.Empty;
        StatusText = status;
        return cancel;
    }

    /// <summary>Ends a run.</summary>
    /// <param name="cancel">The source of the run's token.</param>
    private void End(CancellationTokenSource cancel)
    {
        Volatile.Write(ref _cancel, null);
        cancel.Dispose();
        IsRunning = false;
    }

    /// <summary>Shows a saved copy's summary and opens the copy.</summary>
    /// <param name="outcome">The outcome.</param>
    /// <param name="path">Where the copy was saved.</param>
    private void ShowSaved(SigningOutcome outcome, string path)
    {
        SavedPath = path;
        ProgressPercent = FullBar;
        StatusText = "Done. The signed copy is saved.";
        ResultText = OptimizeReportText.Join(SignedCopyFile.Lines(outcome, path));
        Discard();
        _openCopy?.Invoke(path);
    }
}
