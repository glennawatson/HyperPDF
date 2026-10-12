// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Ocr;
using ReactiveUI;
using ReactiveUI.Binding;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.App.ViewModels;

/// <summary>One language in the text recognition language list: whether it is used, whether its pack is on this computer, and buttons to download or remove it.</summary>
[DebuggerDisplay("OcrLanguageItemViewModel: {Pack.Code} selected={IsSelected} downloaded={IsDownloaded}")]
public sealed partial class OcrLanguageItemViewModel : ReactiveObject
{
    /// <summary>The list that owns this language and does the downloading.</summary>
    private readonly OcrLanguagesViewModel _owner;

    /// <summary>Emits whether the pack can be downloaded now; enables Download.</summary>
    private readonly IObservable<bool> _canDownload;

    /// <summary>Emits whether the pack can be removed now; enables Remove.</summary>
    private readonly IObservable<bool> _canRemove;

    /// <summary>Initializes a new instance of the <see cref="OcrLanguageItemViewModel"/> class.</summary>
    /// <param name="owner">The list that owns this language.</param>
    /// <param name="pack">The language pack.</param>
    /// <param name="selected">Whether the language is used for recognition.</param>
    public OcrLanguageItemViewModel(OcrLanguagesViewModel owner, OcrLanguagePack pack, bool selected)
    {
        ArgumentNullException.ThrowIfNull(owner);
        ArgumentNullException.ThrowIfNull(pack);
        _owner = owner;
        Pack = pack;
        IsSelected = selected;
        _canDownload = this.WhenChanged(static vm => vm.CanDownload);
        _canRemove = this.WhenChanged(static vm => vm.CanRemove);
    }

    /// <summary>Gets the language pack.</summary>
    public OcrLanguagePack Pack { get; }

    /// <summary>Gets the language's name, for example "German".</summary>
    public string Name => Pack.Name;

    /// <summary>Gets or sets a value indicating whether the language is used when recognising text.</summary>
    [Reactive]
    public partial bool IsSelected { get; set; }

    /// <summary>Gets a value indicating whether the pack is downloaded into the app's language folder.</summary>
    [Reactive]
    public partial bool IsDownloaded { get; internal set; }

    /// <summary>Gets a short status, for example "Downloaded" or "Not downloaded, about 2 MB".</summary>
    [Reactive]
    public partial string StatusText { get; internal set; } = string.Empty;

    /// <summary>Gets a value indicating whether Download is offered: the pack is missing and nothing is downloading.</summary>
    [Reactive]
    public partial bool CanDownload { get; internal set; }

    /// <summary>Gets a value indicating whether Remove is offered: the pack is downloaded and nothing is downloading.</summary>
    [Reactive]
    public partial bool CanRemove { get; internal set; }

    /// <summary>Downloads this language's pack.</summary>
    /// <returns>A task.</returns>
    [ReactiveCommand(CanExecute = nameof(_canDownload))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private Task DownloadAsync() => _owner.DownloadAsync([Pack]);

    /// <summary>Deletes this language's downloaded pack.</summary>
    [ReactiveCommand(CanExecute = nameof(_canRemove))]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Remove() => _owner.Remove(this);
}
