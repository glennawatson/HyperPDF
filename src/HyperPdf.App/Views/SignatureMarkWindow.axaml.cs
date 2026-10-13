// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using Avalonia.Platform.Storage;
using HyperPdf.App.ViewModels;
using HyperPdf.Core.Annotations;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>Makes a signature or initials: typed, drawn or from a picture. Closes with <see langword="true"/> to use it.</summary>
[DebuggerDisplay("SignatureMarkWindow: {Title}")]
public sealed partial class SignatureMarkWindow : ReactiveUI.Avalonia.ReactiveWindow<SignatureMarkViewModel>
{
    /// <summary>Initializes a new instance of the <see cref="SignatureMarkWindow"/> class.</summary>
    public SignatureMarkWindow()
    {
        InitializeComponent();
        FieldLabels.Link((MarkTextBox, TextLabel));
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Title, static v => v.Title));
            disposables.Add(this.Bind(ViewModel, static vm => vm.IsTyped, static v => v.TypeOption.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.IsDrawn, static v => v.DrawOption.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.IsImage, static v => v.ImageOption.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Style, static v => v.TypePanel.IsVisible, static style => style == SignatureMarkStyle.Typed));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Style, static v => v.DrawPanel.IsVisible, static style => style == SignatureMarkStyle.Drawn));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Style, static v => v.ImagePanel.IsVisible, static style => style == SignatureMarkStyle.Image));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.TextLabel, static v => v.TextLabel.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.TextLabel, static v => v.MarkTextBox.PlaceholderText));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Text, static v => v.MarkTextBox.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Drawing, static v => v.Pad.Mark));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.AddStrokeCommand, static v => v.Pad.StrokeCommand));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ClearDrawingCommand, static v => v.ClearDrawingButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ChooseImageCommand, static v => v.ChooseImageButton));
            disposables.Add(this.BindInteraction(ViewModel, static vm => vm.BrowseImageInteraction, BrowseAsync));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.SourceImage, static v => v.ImageNameText.Text, static image => image?.Name ?? "No picture chosen yet."));
            disposables.Add(this.Bind(ViewModel, static vm => vm.RemovePaper, static v => v.RemovePaperCheck.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ImageError, static v => v.ImageErrorText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.ImageError, static v => v.ImageErrorText.IsVisible, static error => error is not null));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Preview, static v => v.Preview.Mark));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Remember, static v => v.RememberCheck.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.HasSaved, static v => v.ForgetButton.IsVisible));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.ForgetCommand, static v => v.ForgetButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.UseText, static v => v.UseButton.Content));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.UseCommand, static v => v.UseButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.CancelCommand, static v => v.CancelButton));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Answered)
                .SwitchMap(static answered => answered)
                .SubscribeSafe(answer => Close(answer), OnError));

            // Start on the chosen way: the text box when typing, otherwise the way's option.
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Style)
                .Take(1)
                .SubscribeSafe(style => FocusStart(this, style), OnError));
        });
    }

    /// <summary>Focuses where the user starts for a way of making the mark.</summary>
    /// <param name="window">The window.</param>
    /// <param name="style">The way chosen.</param>
    private static void FocusStart(SignatureMarkWindow window, SignatureMarkStyle style) => _ = style switch
    {
        SignatureMarkStyle.Drawn => window.DrawOption.Focus(),
        SignatureMarkStyle.Image => window.ImageOption.Focus(),
        _ => window.MarkTextBox.Focus(),
    };

    /// <summary>Reports a failure in a subscription.</summary>
    /// <param name="error">The error.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static void OnError(Exception error) => Trace.TraceError(error.ToString());

    /// <summary>Asks for a picture through the desktop's open dialog.</summary>
    /// <param name="context">The interaction context.</param>
    /// <returns>A task.</returns>
    private async Task BrowseAsync(IInteractionContext<RxVoid, string?> context)
    {
        var files = await StorageProvider.OpenFilePickerAsync(new()
        {
            Title = "Choose a Picture of Your Signature",
            AllowMultiple = false,
            FileTypeFilter = [new("Pictures (.png, .jpg, .bmp, .webp)") { Patterns = ["*.png", "*.jpg", "*.jpeg", "*.bmp", "*.webp"] }],
        });
        context.SetOutput(files.Count > 0 ? files[0].TryGetLocalPath() : null);
    }
}
