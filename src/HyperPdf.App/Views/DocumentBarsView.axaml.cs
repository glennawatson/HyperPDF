// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using HyperPdf.App.Controls;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>The bars that describe the document itself: whether it is digitally signed, and which colour separation the page is.</summary>
[DebuggerDisplay("DocumentBarsView: {ViewModel}")]
public sealed partial class DocumentBarsView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="DocumentBarsView"/> class.</summary>
    public DocumentBarsView()
    {
        InitializeComponent();
        SeparationBox.ItemTemplate = new FuncDataTemplate<string>(static (text, _) => new TextBlock { Text = text });
        FieldLabels.Link((SeparationBox, SeparationChoiceLabel));
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(UiText.CultureChanges.SubscribeSafe(_ => RefreshCultureText(), ErrorTrace.Write));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Signatures.SignatureCount, static v => v.SignedBar.IsVisible, static count => count > 0));
            disposables.Add(this.OneWayBind(
                ViewModel,
                static vm => vm.Signatures.SignatureCount,
                static v => v.SignedText.Text,
                static count => count == 1 ? UiText.Get("This document is digitally signed.") : UiText.Format("This document has {0} digital signatures.", count)));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Signatures.CheckCommand, static v => v.CheckSignaturesButton));
            disposables.Add(this.OneWayBind(
                ViewModel,
                static vm => vm.Signatures.IsChecking,
                static v => v.CheckSignaturesButton.Content,
                static checking => checking ? UiText.Get("Checking…") : UiText.Get("Check Signatures")));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Signatures.ShowInteraction)
                .ShowDialogOver(this, static signatures => new SignaturesWindow { ViewModel = signatures }));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Separations.HasSeparations, static v => v.SeparationBar.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Separations.Description, static v => v.SeparationText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Separations.Colorants, static v => v.SeparationBox.ItemsSource));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Separations.Colorants, static v => v.SeparationBox.IsEnabled, static colorants => colorants.Count > 0));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Separations.SelectedIndex, static v => v.SeparationBox.SelectedIndex));
        });
    }

    /// <summary>Refreshes translated presentation text without notifying or changing document state.</summary>
    private void RefreshCultureText()
    {
        if (ViewModel is not { } model)
        {
            return;
        }

        SignedText.Text = model.Signatures.SignatureCount == 1
            ? UiText.Get("This document is digitally signed.")
            : UiText.Format("This document has {0} digital signatures.", model.Signatures.SignatureCount);
        CheckSignaturesButton.Content = model.Signatures.IsChecking ? UiText.Get("Checking…") : UiText.Get("Check Signatures");
    }
}
