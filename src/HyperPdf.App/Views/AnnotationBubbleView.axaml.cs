// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Windows.Input;
using Avalonia.Controls;
using Avalonia.Input;
using HyperPdf.App.Controls;
using HyperPdf.App.Services;
using HyperPdf.App.ViewModels;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.Primitives;

namespace HyperPdf.App.Views;

/// <summary>
/// The on-page comment bubble placed beside a sticky note or commented annotation when the reader clicks it, allowing
/// notes and replies to be read, edited, added or deleted directly on the page.
/// </summary>
[DebuggerDisplay("AnnotationBubbleView: {ViewModel}")]
public sealed partial class AnnotationBubbleView : ReactiveUI.Avalonia.ReactiveUserControl<DocumentTabViewModel>
{
    /// <summary>The width of the bubble card in device independent pixels.</summary>
    private const double CardWidth = 300;

    /// <summary>The gap between the annotation and its bubble card.</summary>
    private const double BubbleGap = 8;

    /// <summary>The minimum margin kept from the canvas edges.</summary>
    private const double EdgeMargin = 8;

    /// <summary>The key binding that saves the note with Ctrl+Enter inside the note editor.</summary>
    private readonly KeyBinding _saveNoteKey = new() { Gesture = new(Key.Enter, KeyModifiers.Control) };

    /// <summary>The key binding that sends a reply with Ctrl+Enter inside the reply editor.</summary>
    private readonly KeyBinding _sendReplyKey = new() { Gesture = new(Key.Enter, KeyModifiers.Control) };

    /// <summary>Initializes a new instance of the <see cref="AnnotationBubbleView"/> class.</summary>
    public AnnotationBubbleView()
    {
        InitializeComponent();
        NoteEditor.KeyBindings.Add(_saveNoteKey);
        ReplyEditor.KeyBindings.Add(_sendReplyKey);
        var tabs = this.WhenChanged(static v => v.ViewModel);
        _ = this.WhenActivated(disposables =>
        {
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.BubbleNoteText, static v => v.NoteEditor.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.Bind(ViewModel, static vm => vm.Annotations.BubbleReplyText, static v => v.ReplyEditor.Text, static text => text, static text => text ?? string.Empty));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.ShowBubbleReplies, static v => v.RepliesText.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.IsReplyingInBubble, static v => v.ReplyPanel.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.CanReplyInBubble, static v => v.ReplyBubbleButton.IsVisible));
            disposables.Add(this.OneWayBind(ViewModel, static vm => vm.Annotations.IsExistingBubble, static v => v.DeleteBubbleButton.IsVisible));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SaveBubbleCommand, static v => v.SaveBubbleButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.StartBubbleReplyCommand, static v => v.ReplyBubbleButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.SendBubbleReplyCommand, static v => v.SendReplyButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.DeleteBubbleAnnotationCommand, static v => v.DeleteBubbleButton));
            disposables.Add(this.BindCommand(ViewModel, static vm => vm.Annotations.CloseBubbleCommand, static v => v.CloseBubbleButton));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Annotations.SaveBubbleCommand)
                .Select(static command => (ICommand?)command).BindTo(_saveNoteKey, static key => key.Command));
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Annotations.SendBubbleReplyCommand)
                .Select(static command => (ICommand?)command).BindTo(_sendReplyKey, static key => key.Command));
            disposables.Add(BubbleCard.KeyDowns.Escape.MarkHandled().InjectLatest(tabs).SubscribeSafe(static tab => tab.Annotations.CloseBubble(), ErrorTrace.Write));
            var bubbles = this.WhenChanged(static v => v.ViewModel!.Annotations.ActiveBubble);
            disposables.Add(bubbles.SubscribeSafe(ShowBubble, ErrorTrace.Write));
            disposables.Add(bubbles.KeepNotNull().ObserveOn(RxSchedulers.MainThreadScheduler).SelectWith(NoteEditor, static (editor, _) => editor).FocusAtEnd());
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Annotations.IsReplyingInBubble)
                .Where(static replying => replying).ObserveOn(RxSchedulers.MainThreadScheduler).SelectWith(ReplyEditor, static (editor, _) => editor).FocusAtEnd());
            disposables.Add(this.WhenChanged(static v => v.ViewModel!.Zoom, static v => v.ViewModel!.Rotation, static (zoom, _) => zoom)
                .AsVoid.Merge(Pages!.WhenChanged(static c => c.PageScale).AsVoid).SubscribeSafe(_ => PlaceBubble(), ErrorTrace.Write));
        });
    }

    /// <summary>Gets or sets the page canvas the bubble sits over.</summary>
    internal PageCanvas? Pages { get; set; }

    /// <summary>Shows or hides the bubble card and updates its heading, swatch and replies.</summary>
    /// <param name="bubble">The active bubble session, or <see langword="null"/>.</param>
    private void ShowBubble(AnnotationBubbleSession? bubble)
    {
        Pages?.InvalidateVisual();
        if (bubble is null)
        {
            var hadFocus = BubbleCard.IsKeyboardFocusWithin;
            BubbleCard.IsVisible = false;
            if (hadFocus)
            {
                _ = Pages?.Focus();
            }

            return;
        }

        ColorSwatch.Background = ColourBrushes.Opaque(bubble.Color);
        HeadingText.Text = bubble.Heading;
        RepliesText.Text = bubble.RepliesText;
        BubbleCard.IsVisible = true;
        PlaceBubble();
    }

    /// <summary>Places the bubble card beside its annotation on the page canvas.</summary>
    private void PlaceBubble()
    {
        if (ViewModel?.Annotations.ActiveBubble is not { } bubble || Pages is not { } pages)
        {
            return;
        }

        var rect = pages.GetCanvasRect(bubble.PageIndex, bubble.Bounds);
        var left = rect.Right + BubbleGap;
        if (pages.Bounds.Width > CardWidth + EdgeMargin + EdgeMargin && left + CardWidth + EdgeMargin > pages.Bounds.Width)
        {
            left = Math.Max(EdgeMargin, rect.Left - CardWidth - BubbleGap);
        }

        var top = Math.Max(EdgeMargin, rect.Top);
        Canvas.SetLeft(this, left);
        Canvas.SetTop(this, top);
    }
}
