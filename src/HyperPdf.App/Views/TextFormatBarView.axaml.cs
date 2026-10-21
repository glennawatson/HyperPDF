// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Globalization;
using System.Runtime.CompilerServices;
using Avalonia.Controls;
using Avalonia.Controls.Templates;
using HyperPdf.App.Controls;
using HyperPdf.Core.Text;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;

namespace HyperPdf.App.Views;

/// <summary>The text format bar: font, size, style, alignment, colour and spacing of the text being typed, which changes as the bar does.</summary>
[DebuggerDisplay("TextFormatBarView: {ViewModel}")]
public sealed partial class TextFormatBarView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>The style class of the editor for text typed on the page.</summary>
    private const string PageTextClass = "pagetext";

    /// <summary>Initializes a new instance of the <see cref="TextFormatBarView"/> class.</summary>
    public TextFormatBarView()
    {
        InitializeComponent();
        FontFamilyBox.ItemTemplate = new FuncDataTemplate<string>((name, _) => new TextBlock
        {
            Text = PageFonts.Label(name, ViewModel?.Services.LoadedFontCatalog),
            FontFamily = PageFonts.Get(ViewModel?.Services.PageFontFamilies, name),
        });
        FontSizeBox.ItemsSource = SizeChoices();
        FieldLabels.Link((FontFamilyBox, FontFamilyLabel), (FontSizeBox, FontSizeLabel));
        var tabs = this.WhenChanged(static v => v.ViewModel);
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.IsTextFormatShown, static v => v.TextFormatBar.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.FontFamilies, static v => v.FontFamilyBox.ItemsSource));
            disposables.Add(this.Bind(
                ViewModel,
                static vm => vm.Annotations.FontFamily,
                static v => v.FontFamilyBox.SelectedItem,
                static family => family,
                static item => item as string ?? StandardFontFamilies.Sans));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.FontSize, static v => v.FontSizeBox.Text, SizeText));
            disposables.Add(FontSizeBox.Entries.Inject(tabs).Select(ApplyFontSize).KeepNotNull().BindTo(this, static v => v.FontSizeBox.Text));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.ShrinkTextCommand, static v => v.SmallerTextButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.GrowTextCommand, static v => v.LargerTextButton));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsBold, static v => v.BoldToggle.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsItalic, static v => v.ItalicToggle.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsUnderline, static v => v.UnderlineToggle.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsAlignedLeft, static v => v.AlignLeftToggle.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsAlignedCenter, static v => v.AlignCenterToggle.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.IsAlignedRight, static v => v.AlignRightToggle.IsChecked, static on => on, static on => on == true));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.TextColorName, static v => v.TextColourText.Text));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.TextColor, static v => v.TextColourSwatch.Background, ColourBrushes.Opaque));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetTextColorCommand, static v => v.BlackTextItem, "Black".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetTextColorCommand, static v => v.DarkBlueTextItem, "Dark blue".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetTextColorCommand, static v => v.BlueTextItem, "Blue".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetTextColorCommand, static v => v.RedTextItem, "Red".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SetTextColorCommand, static v => v.GreenTextItem, "Green".AsCommandParameter));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.EditTextPropertiesCommand, static v => v.TextPropertiesButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.FinishTextCommand, static v => v.FinishTextButton));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.IsEditingText, static v => v.FinishTextButton.IsEnabled));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Annotations.TextPropertiesInteraction)
                .AskDialogOver(this, static properties => new TextPropertiesWindow { ViewModel = properties }));
            disposables.Add(FontFamilyBox.DropDownOpenings.InjectLatest(tabs).SubscribeSafe(static tab => tab.Annotations.RefreshFontFamilies(), ErrorTrace.Write));

            // The text typed on the page hands the keyboard to the bar's control; a format change made with it gives the
            // keyboard back to that text once the click is done, so typing carries on.
            var typedText = TextFormatBar.GotFocuses
                .Select(static args => args.OldFocusedElement as TextBox)
                .KeepNotNull()
                .Where(static box => box.Classes.Contains(PageTextClass));
            disposables.Add(Signal.Merge(FontFamilyBox.SelectionChanges.AsVoid, TextFormatBar.DescendantClicks.AsVoid)
                .InjectLatest(tabs)
                .Where(static tab => tab.Annotations.IsEditingText)
                .WithLatestFrom(typedText, static (_, box) => box)
                .ObserveOn(RxSchedulers.MainThreadScheduler)
                .FocusAtEnd());
        });
    }

    /// <summary>Formats a text size for the size box.</summary>
    /// <param name="size">The size in points.</param>
    /// <returns>The text.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string SizeText(float size) => size.ToString("0.#", CultureInfo.CurrentCulture);

    /// <summary>Lists the sizes the size box offers.</summary>
    /// <returns>The sizes as text.</returns>
    private static string[] SizeChoices()
    {
        var sizes = new string[AnnotationsViewModel.TextSizes.Count];
        for (var i = 0; i < sizes.Length; i++)
        {
            sizes[i] = SizeText(AnnotationsViewModel.TextSizes[i]);
        }

        return sizes;
    }

    /// <summary>Sets the text size from what was typed or picked in the size box.</summary>
    /// <param name="entry">The size box's text, and the tab being edited.</param>
    /// <returns>The size as the box should show it, or <see langword="null"/> when the text is not a size.</returns>
    private static string? ApplyFontSize(Injected<string?, DocumentTabViewModel> entry)
    {
        var (text, tab) = entry;
        if (!float.TryParse(text, NumberStyles.Float, CultureInfo.CurrentCulture, out var size) || !float.IsFinite(size))
        {
            return null;
        }

        tab.Annotations.FontSize = Math.Clamp(size, TextFormat.MinFontSize, TextFormat.MaxFontSize);
        return SizeText(tab.Annotations.FontSize);
    }
}
