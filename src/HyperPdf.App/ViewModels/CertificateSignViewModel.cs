// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.Core.Settings;
using HyperPdf.Core.Signatures.Signing;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Disposables;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>
/// The "Sign with Certificate" window: a certificate file (PKCS #12, .p12 or .pfx), its password, and an optional reason
/// and place. Signing checks the password straight away, so a mistake is shown in the window rather than later. The
/// user chooses whether the certificate is remembered, and can forget each remembered one; the password never is.
/// </summary>
[DebuggerDisplay("CertificateSignViewModel: {CertificatePath}")]
public sealed partial class CertificateSignViewModel : ReactiveObject, IDisposable
{
    /// <summary>The distance of a visible signature from the page's edges, in points.</summary>
    private const double VisibleMargin = 36;

    /// <summary>The width of a visible signature, in points.</summary>
    private const double VisibleWidth = 200;

    /// <summary>The height of a visible signature, in points.</summary>
    private const double VisibleHeight = 72;

    /// <summary>The bottom left corner of the page, where a visible signature goes.</summary>
    private static readonly SigningArea VisibleArea = new(VisibleMargin, VisibleMargin, VisibleMargin + VisibleWidth, VisibleMargin + VisibleHeight);

    /// <summary>Raises <see cref="Answered"/> with <see langword="true"/>.</summary>
    private readonly Signal<RxVoid> _confirmed = new();

    /// <summary>Whether a certificate file is named, so signing can start.</summary>
    private readonly IObservable<bool> _canSign;

    /// <summary>Whether a remembered certificate is chosen, so it can be forgotten.</summary>
    private readonly IObservable<bool> _canForget;

    /// <summary>The subscriptions this window's state owns.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>Works out what signing would do for a request, or returns <see langword="null"/> when the document cannot say.</summary>
    private readonly Func<SigningRequest, SigningPreview?>? _previewer;

    /// <summary>The page a new signature field goes on.</summary>
    private readonly int _pageIndex;

    /// <summary>The address of the timestamp server from Preferences; empty for none.</summary>
    private readonly string _timestampServer;

    /// <summary>The loaded certificate, once the password was right.</summary>
    private X509Certificate2? _certificate;

    /// <summary>The last preview, kept so signing can be refused with its reasons.</summary>
    private SigningPreview? _preview;

    /// <summary>Initializes a new instance of the <see cref="CertificateSignViewModel"/> class.</summary>
    /// <param name="remembered">The certificates the user chose to remember, most recently used first.</param>
    public CertificateSignViewModel(IReadOnlyList<RememberedCertificate> remembered)
        : this(remembered, 0, null, string.Empty)
    {
    }

    /// <summary>Initializes a new instance of the <see cref="CertificateSignViewModel"/> class.</summary>
    /// <param name="remembered">The certificates the user chose to remember, most recently used first.</param>
    /// <param name="pageIndex">The page a new signature field goes on.</param>
    /// <param name="previewer">Works out what signing would do for a request, or <see langword="null"/> for no preview.</param>
    /// <param name="timestampServer">The address of the timestamp server from Preferences; empty for none.</param>
    public CertificateSignViewModel(IReadOnlyList<RememberedCertificate> remembered, int pageIndex, Func<SigningRequest, SigningPreview?>? previewer, string timestampServer)
    {
        ArgumentNullException.ThrowIfNull(remembered);
        _pageIndex = pageIndex;
        _previewer = previewer;
        _timestampServer = timestampServer;
        Remembered = [.. remembered];
        HasRemembered = Remembered.Count > 0;
        _canSign = this.WhenChanged(static vm => vm.CertificatePath).Select(static path => !string.IsNullOrWhiteSpace(path));
        _canForget = this.WhenChanged(static vm => vm.SelectedCertificate).Select(static chosen => chosen is not null);
        Answered = Signal.Merge(_confirmed.Select(static _ => true), CancelCommand);

        // Choosing a remembered certificate fills in its file. The box shows whether the named file is remembered, so a
        // newly chosen file is only remembered when the user ticks it.
        _subscriptions.Add(this.WhenChanged(static vm => vm.SelectedCertificate).SubscribeSafe(UseRemembered, OnError));
        _subscriptions.Add(this.WhenChanged(static vm => vm.CertificatePath).SubscribeSafe(path => Remember = IsRemembered(path), OnError));
        SelectedCertificate = Remembered.Count > 0 ? Remembered[0] : null;

        // What will be signed is described again whenever a choice that changes it changes.
        _subscriptions.Add(this.WhenChanged(static vm => vm.Reason).SubscribeSafe(_ => UpdatePreview(), OnError));
        _subscriptions.Add(this.WhenChanged(static vm => vm.Location).SubscribeSafe(_ => UpdatePreview(), OnError));
        _subscriptions.Add(this.WhenChanged(static vm => vm.CertificationIndex).SubscribeSafe(_ => UpdatePreview(), OnError));
        _subscriptions.Add(this.WhenChanged(static vm => vm.IsVisibleSignature).SubscribeSafe(_ => UpdatePreview(), OnError));
    }

    /// <summary>Gets the kinds of signature the user can choose between.</summary>
    public IReadOnlyList<string> CertificationChoices => SigningPreviewText.CertificationChoices;

    /// <summary>Gets or sets the index of the chosen kind of signature, in the order of <see cref="CertificationLevel"/>.</summary>
    [Reactive]
    public partial int CertificationIndex { get; set; }

    /// <summary>Gets or sets a value indicating whether the signature is drawn on the page.</summary>
    [Reactive]
    public partial bool IsVisibleSignature { get; set; }

    /// <summary>Gets what signing would do, in plain words, one sentence per line.</summary>
    [Reactive]
    public partial string PreviewText { get; private set; } = string.Empty;

    /// <summary>Gets a value indicating whether a preview is available.</summary>
    public bool HasPreview => _previewer is not null;

    /// <summary>Gets the certificates the user chose to remember.</summary>
    public ObservableCollection<RememberedCertificate> Remembered { get; }

    /// <summary>Gets a value indicating whether any certificate is remembered.</summary>
    [Reactive]
    public partial bool HasRemembered { get; private set; }

    /// <summary>Gets or sets the remembered certificate chosen, or <see langword="null"/>.</summary>
    [Reactive]
    public partial RememberedCertificate? SelectedCertificate { get; set; }

    /// <summary>Gets or sets the certificate file.</summary>
    [Reactive]
    public partial string CertificatePath { get; set; } = string.Empty;

    /// <summary>Gets or sets the certificate's password. It is cleared once the certificate is loaded and never saved.</summary>
    [Reactive]
    public partial string Password { get; set; } = string.Empty;

    /// <summary>Gets or sets a value indicating whether the certificate is remembered on this computer for next time.</summary>
    [Reactive]
    public partial bool Remember { get; set; }

    /// <summary>Gets or sets why the document is signed; optional.</summary>
    [Reactive]
    public partial string Reason { get; set; } = string.Empty;

    /// <summary>Gets or sets where it is signed; optional.</summary>
    [Reactive]
    public partial string Location { get; set; } = string.Empty;

    /// <summary>Gets what went wrong with the certificate, or <see langword="null"/>.</summary>
    [Reactive]
    public partial string? Error { get; private set; }

    /// <summary>Gets the interaction asking for a certificate file.</summary>
    public Interaction<RxVoid, string?> BrowseInteraction { get; } = new();

    /// <summary>Gets the answer, which closes the window: <see langword="true"/> to sign.</summary>
    public IObservable<bool> Answered { get; }

    /// <summary>Builds the request for what is chosen.</summary>
    /// <param name="time">The signing time.</param>
    /// <returns>The request.</returns>
    public SigningRequest BuildRequest(DateTimeOffset time) => new(_pageIndex, Reason, Location, time)
    {
        Certification = (CertificationLevel)Math.Clamp(CertificationIndex, 0, SigningPreviewText.CertificationChoices.Length - 1),
        Area = IsVisibleSignature ? VisibleArea : default,
    };

    /// <summary>Hands over the loaded certificate; the caller disposes it.</summary>
    /// <returns>The certificate, or <see langword="null"/> when none was loaded.</returns>
    public X509Certificate2? TakeCertificate()
    {
        var certificate = _certificate;
        _certificate = null;
        return certificate;
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        _subscriptions.Dispose();
        _certificate?.Dispose();
        _confirmed.Dispose();
    }

    /// <summary>Closes the window without signing.</summary>
    /// <returns>Always <see langword="false"/>.</returns>
    [ReactiveCommand]
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static bool Cancel() => false;

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Describes what signing would do for what is chosen now.</summary>
    private void UpdatePreview()
    {
        if (_previewer is null)
        {
            return;
        }

        try
        {
            _preview = _previewer(BuildRequest(TimeProvider.System.GetUtcNow()));
        }
        catch (InvalidOperationException)
        {
            // The document was closed or reloaded under the window, so there is nothing to describe.
            _preview = null;
            PreviewText = CopyFailure.DocumentClosedText;
            return;
        }

        PreviewText = SigningPreviewText.Describe(_preview, _timestampServer);
    }

    /// <summary>Fills in a chosen remembered certificate.</summary>
    /// <param name="chosen">The certificate, or <see langword="null"/>.</param>
    private void UseRemembered(RememberedCertificate? chosen)
    {
        if (chosen is not null)
        {
            CertificatePath = chosen.Path;
        }
    }

    /// <summary>Determines whether a certificate file is remembered.</summary>
    /// <param name="path">The file.</param>
    /// <returns><see langword="true"/> when it is in the remembered list.</returns>
    private bool IsRemembered(string path)
    {
        foreach (var remembered in Remembered)
        {
            if (string.Equals(remembered.Path, path, StringComparison.Ordinal))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Loads the certificate with the password, confirming when it can sign.</summary>
    [ReactiveCommand(CanExecute = nameof(_canSign))]
    private void Sign()
    {
        UpdatePreview();
        if (_preview is { CanSign: false } refused)
        {
            Error = string.Join(' ', refused.Problems);
            return;
        }

        try
        {
            var certificate = X509CertificateLoader.LoadPkcs12FromFile(CertificatePath, Password);
            if (!certificate.HasPrivateKey)
            {
                certificate.Dispose();
                Error = "This certificate has no private key, so it cannot sign.";
                return;
            }

            _certificate?.Dispose();
            _certificate = certificate;
            Error = null;

            // The password has done its job; drop it rather than keep it for the life of the window.
            Password = string.Empty;
            _confirmed.OnNext(RxVoid.Default);
        }
        catch (CryptographicException)
        {
            Error = "The password is not right, or the file is not a certificate.";
        }
        catch (IOException ex)
        {
            Error = ex.Message;
        }
        catch (UnauthorizedAccessException ex)
        {
            Error = ex.Message;
        }
    }

    /// <summary>Forgets the chosen remembered certificate. The certificate file itself is left alone.</summary>
    [ReactiveCommand(CanExecute = nameof(_canForget))]
    private void Forget()
    {
        if (SelectedCertificate is not { } chosen)
        {
            return;
        }

        _ = Remembered.Remove(chosen);
        HasRemembered = Remembered.Count > 0;
        SelectedCertificate = null;
        Remember = IsRemembered(CertificatePath);
    }

    /// <summary>Asks for a certificate file.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand]
    private async Task BrowseAsync()
    {
        if (await BrowseInteraction.Handle(RxVoid.Default).ToTask().ConfigureAwait(true) is not { Length: > 0 } path)
        {
            return;
        }

        SelectedCertificate = null;
        CertificatePath = path;
    }
}
