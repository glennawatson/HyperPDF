// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.App.Services;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Settings;
using HyperPdf.Core.Signatures;
using HyperPdf.Core.Signatures.Signing;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// A tab's digital signatures. Signatures are only checked when the user asks, because checking trust may look up
/// certificate revocation online; the check runs off the UI thread.
/// </summary>
[DebuggerDisplay("SignaturesViewModel: {SignatureCount} signatures")]
public sealed partial class SignaturesViewModel : ReactiveObject
{
    /// <summary>The owning tab.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>The application services.</summary>
    private readonly AppServices _services;

    /// <summary>Initializes a new instance of the <see cref="SignaturesViewModel"/> class.</summary>
    /// <param name="owner">The owning tab.</param>
    /// <param name="services">The application services.</param>
    public SignaturesViewModel(DocumentTabViewModel owner, AppServices services) => (_owner, _services) = (owner, services);

    /// <summary>Gets the interaction showing the "Sign with Certificate" window; the output says whether to sign.</summary>
    public Interaction<CertificateSignViewModel, bool> CertificateSignInteraction { get; } = new();

    /// <summary>Gets the interaction asking the view to show the checked signatures.</summary>
    public Interaction<SignaturesViewModel, RxVoid> ShowInteraction { get; } = new();

    /// <summary>Gets the checked signatures.</summary>
    public ObservableCollection<DocumentSignature> Signatures { get; } = [];

    /// <summary>Gets the number of signatures in the document.</summary>
    [Reactive]
    public partial int SignatureCount { get; private set; }

    /// <summary>Gets a value indicating whether a check is running.</summary>
    [Reactive]
    public partial bool IsChecking { get; private set; }

    /// <summary>Gets the document's file name, for the window title.</summary>
    public string FileName => _owner.FileName;

    /// <summary>Counts the document's signatures; called when the document loads.</summary>
    public void Refresh()
    {
        SignatureCount = DocumentFeature<ISignatureSource>.Get(_owner.TryGetDocument())?.SignatureCount ?? 0;
        Signatures.Clear();
    }

    /// <summary>Asks the signatures window to close.</summary>
    [ReactiveCommand]
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void Close()
    {
    }

    /// <summary>Determines whether an exception is a signing failure to tell the user about, rather than a bug.</summary>
    /// <param name="exception">The exception.</param>
    /// <returns><see langword="true"/> for a damaged file, a certificate problem, a file error or an unreachable timestamp server.</returns>
    private static bool IsSigningFailure(Exception exception) =>
        exception is InvalidDataException or SigningRefusedException or NotSupportedException or CryptographicException or IOException or UnauthorizedAccessException
            or HttpRequestException or TaskCanceledException or InvalidOperationException;

    /// <summary>Checks every signature against the file.</summary>
    /// <param name="raw">The signatures as stored.</param>
    /// <param name="path">The file.</param>
    /// <returns>The checked signatures.</returns>
    private static DocumentSignature[] VerifyAll(IReadOnlyList<RawSignature> raw, string path)
    {
        var results = new DocumentSignature[raw.Count];
        var store = DocumentSecurityStore.Read(File.ReadAllBytes(path));
        for (var i = 0; i < results.Length; i++)
        {
            results[i] = SignatureVerifier.Verify(raw[i], path, [], store);
        }

        return results;
    }

    /// <summary>Finds a remembered certificate by its file.</summary>
    /// <param name="remembered">The remembered certificates.</param>
    /// <param name="path">The file.</param>
    /// <returns>The index, or -1.</returns>
    private static int IndexOf(List<RememberedCertificate> remembered, string path)
    {
        for (var i = 0; i < remembered.Count; i++)
        {
            if (string.Equals(remembered[i].Path, path, StringComparison.Ordinal))
            {
                return i;
            }
        }

        return -1;
    }

    /// <summary>
    /// Saves the user's remember choices from the window: certificates they forgot are dropped even when they cancelled,
    /// and the certificate they signed with is remembered only when they ticked the box. Only the file, the name it
    /// holds and its fingerprint are kept.
    /// </summary>
    /// <param name="request">The window's state.</param>
    /// <param name="certificate">The certificate signed with, or <see langword="null"/> when the user cancelled.</param>
    private void UpdateRemembered(CertificateSignViewModel request, X509Certificate2? certificate)
    {
        var remembered = _services.Settings.RememberedCertificates;
        var changed = !remembered.SequenceEqual(request.Remembered);
        remembered.Clear();
        remembered.AddRange(request.Remembered);
        if (certificate is not null)
        {
            var index = IndexOf(remembered, request.CertificatePath);
            if (index >= 0)
            {
                remembered.RemoveAt(index);
                changed = true;
            }

            if (request.Remember)
            {
                // Most recently used first, so it is offered next time.
                remembered.Insert(0, new(request.CertificatePath, certificate.GetNameInfo(X509NameType.SimpleName, false), certificate.Thumbprint));
                changed = true;
            }
        }

        if (changed)
        {
            _services.SaveSettings();
        }
    }

    /// <summary>Asks for the certificate and where to save, signs a copy off the UI thread and opens it.</summary>
    /// <param name="cancellationToken">Stops signing, including waiting for the timestamp server.</param>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task SignWithCertificateAsync(CancellationToken cancellationToken)
    {
        // The preview asks the tab for its document each time, so a reload while the window is open does not leave it on a closed one.
        using var request = new CertificateSignViewModel(
            _services.Settings.RememberedCertificates,
            Math.Max(0, _owner.CurrentPageIndex),
            r => PdfSigner.Preview(_owner.TryGetDocument(), r),
            _services.Settings.TimestampServer);
        var accepted = await CertificateSignInteraction.Handle(request).ToTask(cancellationToken).ConfigureAwait(true);
        var certificate = accepted ? request.TakeCertificate() : null;
        UpdateRemembered(request, certificate);
        if (certificate is null)
        {
            return;
        }

        using (certificate)
        {
            var suggested = $"{Path.GetFileNameWithoutExtension(_owner.FileName)}-signed.pdf";
            var destination = await _owner.SaveAsInteraction.Handle(suggested).ToTask(cancellationToken).ConfigureAwait(true);
            var document = _owner.TryGetDocument();
            if (string.IsNullOrEmpty(destination) || document is null)
            {
                return;
            }

            var signing = request.BuildRequest(TimeProvider.System.GetUtcNow());
            SigningOutcome signed;
            try
            {
                var timestamper = TimestampServers.Create(_services.Settings.TimestampServer);

                // Building and signing the file is CPU work, so it starts on the pool; the timestamp request then awaits.
                // The document is signed with its unsaved edits, and the open document is not changed.
                signed = await Task.Run(() => PdfSigner.SignAsync(document, certificate, signing, timestamper, cancellationToken).AsTask(), cancellationToken).ConfigureAwait(true);
                await SignedCopyFile.WriteAsync(signed, destination, cancellationToken).ConfigureAwait(true);
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception ex) when (IsSigningFailure(ex))
            {
                _owner.Notice = $"Could not sign: {CopyFailure.Describe(ex)}";
                return;
            }

            // Notes tell the user what the signer changed or could not do, such as a missing timestamp or a repair.
            _owner.Notice = $"Signed copy saved as {Path.GetFileName(destination)}.{SignedCopyFile.NotesSuffix(signed)}";
            _services.RequestOpen(destination);
        }
    }

    /// <summary>Checks every signature off the UI thread, then shows the results.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task CheckAsync()
    {
        if (DocumentFeature<ISignatureSource>.Get(_owner.TryGetDocument()) is not
            {
            } source)
        {
            return;
        }

        IsChecking = true;
        try
        {
            var raw = source.GetSignatures();
            var path = _owner.FilePath;
            var checkedSignatures = await Task.Run(() => VerifyAll(raw, path)).ConfigureAwait(true);
            Signatures.Clear();
            foreach (var signature in checkedSignatures)
            {
                Signatures.Add(signature);
            }
        }
        finally
        {
            IsChecking = false;
        }

        _ = await ShowInteraction.Handle(this).ToTask().ConfigureAwait(true);
    }
}
