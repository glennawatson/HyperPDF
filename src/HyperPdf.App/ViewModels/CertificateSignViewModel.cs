// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Security.Cryptography;
using System.Security.Cryptography.X509Certificates;
using HyperPdf.Core.Settings;
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
    /// <summary>Raises <see cref="Answered"/> with <see langword="true"/>.</summary>
    private readonly Signal<RxVoid> _confirmed = new();

    /// <summary>Whether a certificate file is named, so signing can start.</summary>
    private readonly IObservable<bool> _canSign;

    /// <summary>Whether a remembered certificate is chosen, so it can be forgotten.</summary>
    private readonly IObservable<bool> _canForget;

    /// <summary>The subscriptions this window's state owns.</summary>
    private readonly MultipleDisposable _subscriptions = [];

    /// <summary>The loaded certificate, once the password was right.</summary>
    private X509Certificate2? _certificate;

    /// <summary>Initializes a new instance of the <see cref="CertificateSignViewModel"/> class.</summary>
    /// <param name="remembered">The certificates the user chose to remember, most recently used first.</param>
    public CertificateSignViewModel(IReadOnlyList<RememberedCertificate> remembered)
    {
        ArgumentNullException.ThrowIfNull(remembered);
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
    }

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
