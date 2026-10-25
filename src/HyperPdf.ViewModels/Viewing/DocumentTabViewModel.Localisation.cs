// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>Retains localized messages and refreshes the existing document display.</summary>
public sealed partial class DocumentTabViewModel
{
    /// <summary>The existing Media state, created on first use.</summary>
    private DocumentMediaViewModel? _media;

    /// <summary>The existing Parts state, created on first use.</summary>
    private DocumentPartsViewModel? _parts;

    /// <summary>The existing Attachments state, created on first use.</summary>
    private AttachmentsViewModel? _attachments;

    /// <summary>The existing PagePresentation state, created on first use.</summary>
    private PagePresentationViewModel? _pagePresentation;

    /// <summary>The existing Signatures state, created on first use.</summary>
    private SignaturesViewModel? _signatures;

    /// <summary>The existing Forms state, created on first use.</summary>
    private FormsViewModel? _forms;

    /// <summary>The existing FillAndSign state, created on first use.</summary>
    private FillAndSignViewModel? _fillAndSign;

    /// <summary>The existing Printing state, created on first use.</summary>
    private TabPrintingViewModel? _printing;

    /// <summary>The resource identity of the displayed PreviewCaption.</summary>
    private LocalizedMessage _localizedPreviewCaption = LocalizedMessage.Literal(string.Empty);

    /// <summary>The resource identity of the displayed ErrorMessage.</summary>
    private LocalizedMessage _localizedErrorMessage;

    /// <summary>The resource identity of the displayed CertificateError.</summary>
    private LocalizedMessage _localizedCertificateError;

    /// <summary>The resource identity of the displayed Notice.</summary>
    private LocalizedMessage _localizedNotice;

    /// <summary>The save report behind the currently visible save notice.</summary>
    private DocumentSaveOutcome? _saveNotice;

    /// <summary>Sets the displayed PreviewCaption while retaining its resource identity.</summary>
    /// <param name="message">The message.</param>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void SetPreviewCaption(LocalizedMessage message) => this.RaiseAndSetIfChanged(ref _localizedPreviewCaption, message, nameof(PreviewCaption));

    /// <summary>Sets the displayed ErrorMessage while retaining its resource identity.</summary>
    /// <param name="message">The message.</param>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void SetErrorMessage(LocalizedMessage message) => this.RaiseAndSetIfChanged(ref _localizedErrorMessage, message, nameof(ErrorMessage));

    /// <summary>Sets the displayed CertificateError while retaining its resource identity.</summary>
    /// <param name="message">The message.</param>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void SetCertificateError(LocalizedMessage message) => this.RaiseAndSetIfChanged(ref _localizedCertificateError, message, nameof(CertificateError));

    /// <summary>Sets the displayed Notice while retaining its resource identity.</summary>
    /// <param name="message">The message.</param>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    public void SetNotice(LocalizedMessage message)
    {
        _saveNotice = null;
        _ = this.RaiseAndSetIfChanged(ref _localizedNotice, message, nameof(Notice));
    }

    /// <inheritdoc/>
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        this.RaisePropertyChanged(nameof(PreviewCaption));
        this.RaisePropertyChanged(nameof(ErrorMessage));
        this.RaisePropertyChanged(nameof(CertificateError));
        this.RaisePropertyChanged(nameof(Notice));
        LocalisationRefresh.RefreshCore(_services, visited);
        LocalisationRefresh.RefreshCore(_fileWatch, visited);
        LocalisationRefresh.RefreshCore(_copies, visited);
        LocalisationRefresh.RefreshCore(_links, visited);
        LocalisationRefresh.RefreshCore(_readAloud, visited);
        LocalisationRefresh.RefreshCore(_focusMode, visited);
        LocalisationRefresh.RefreshCore(_layers, visited);
        LocalisationRefresh.RefreshCore(_tags, visited);
        LocalisationRefresh.RefreshCore(_separations, visited);
        LocalisationRefresh.RefreshCore(_measure, visited);
        LocalisationRefresh.RefreshCore(_annotations, visited);
        LocalisationRefresh.RefreshCore(_outlineEditing, visited);
        LocalisationRefresh.RefreshCore(_reading, visited);
        LocalisationRefresh.RefreshCore(_search, visited);
        LocalisationRefresh.RefreshCore(_media, visited);
        LocalisationRefresh.RefreshCore(_parts, visited);
        LocalisationRefresh.RefreshCore(_attachments, visited);
        LocalisationRefresh.RefreshCore(_pagePresentation, visited);
        LocalisationRefresh.RefreshCore(_signatures, visited);
        LocalisationRefresh.RefreshCore(_forms, visited);
        LocalisationRefresh.RefreshCore(_fillAndSign, visited);
        LocalisationRefresh.RefreshCore(_printing, visited);
        LocalisationRefresh.RefreshCore(Notices, visited);
        LocalisationRefresh.RefreshCore(TextRecognitionInUse, visited);
        LocalisationRefresh.RefreshCore(Outline, visited);
        LocalisationRefresh.RefreshCore(PagesInUse, visited);
        LocalisationRefresh.RefreshCore(PageTools, visited);
    }
}
