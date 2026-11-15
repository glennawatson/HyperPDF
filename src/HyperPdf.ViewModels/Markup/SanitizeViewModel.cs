// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using HyperPdf.ViewModels.Viewing;
using ReactiveUI;
using ReactiveUI.Primitives;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Markup;

/// <summary>Takes hidden information out of the open document before it is shared: descriptive details, scripts, embedded files and comments.</summary>
[DebuggerDisplay("SanitizeViewModel")]
public sealed partial class SanitizeViewModel : ReactiveObject
{
    /// <summary>The tab whose document is cleaned.</summary>
    private readonly DocumentTabViewModel _owner;

    /// <summary>Initializes a new instance of the <see cref="SanitizeViewModel"/> class.</summary>
    /// <param name="owner">The tab whose document is cleaned.</param>
    public SanitizeViewModel(DocumentTabViewModel owner)
    {
        ArgumentNullException.ThrowIfNull(owner);
        _owner = owner;
        Answered = Signal.Merge(AcceptCommand, CancelCommand);
    }

    /// <summary>Gets the window's title.</summary>
    public string Title { get; } = UiText.Get("Remove Hidden Information");

    /// <summary>Gets the question's answer: whether the settings were accepted.</summary>
    public IObservable<bool> Answered { get; }

    /// <summary>Gets the interaction that shows the window; the output says whether to clean the document.</summary>
    public Interaction<SanitizeViewModel, bool> ShowInteraction { get; } = new();

    /// <summary>Gets or sets a value indicating whether the title, author, subject, keywords, creator and producer are cleared.</summary>
    [Reactive]
    public partial bool Metadata { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether JavaScript and the actions that run it are removed.</summary>
    [Reactive]
    public partial bool Scripts { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether embedded files and file attachment notes are removed.</summary>
    [Reactive]
    public partial bool Attachments { get; set; } = true;

    /// <summary>Gets or sets a value indicating whether notes and marks are removed.</summary>
    [Reactive]
    public partial bool Comments { get; set; }

    /// <summary>Shows the window and cleans the document when the settings are accepted.</summary>
    /// <returns>A task that ends when the window is closed and any change is made.</returns>
    [ReactiveCommand]
    public async Task ShowAsync()
    {
        if (await ShowInteraction.Handle(this).ToTask().ConfigureAwait(true))
        {
            Apply();
        }
    }

    /// <summary>Gives up without changing the document.</summary>
    /// <returns>Always <see langword="false"/>.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Cancel() => false;

    /// <summary>Accepts the settings.</summary>
    /// <returns>Always <see langword="true"/>.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Accept() => true;

    /// <summary>Says what was taken out.</summary>
    /// <param name="summary">What was taken out.</param>
    /// <returns>The words.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static string Describe(SanitizeSummary summary) =>
        UiText.Format(
            "Removed {0} scripts, {1} attachments and {2} comments. The details were cleared: {3}. Undo is in the Pages menu.",
            summary.Scripts,
            summary.Attachments,
            summary.Comments,
            summary.MetadataCleared ? UiText.Get("yes") : UiText.Get("no"));

    /// <summary>Takes the chosen things out of the document and says what was taken out.</summary>
    private void Apply()
    {
        if (DocumentFeature<IDocumentSanitizer>.Get(_owner.TryGetDocument()) is not { } sanitizer)
        {
            _owner.SetNotice(LocalizedMessage.Literal(UiText.Get("This document cannot be changed in this way.")));
            return;
        }

        try
        {
            var summary = sanitizer.Sanitize(new(Metadata, Scripts, Attachments, Comments));
            _owner.Pages.NotifyStructureChanged();
            _owner.SetNotice(LocalizedMessage.Literal(Describe(summary)));
        }
        catch (Exception ex) when (ex is ArgumentException or InvalidOperationException or NotSupportedException)
        {
            _owner.SetNotice(LocalizedMessage.Literal($"{UiText.Get("Could not remove the hidden information.")} {ex.Message}"));
        }
    }
}
