// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using ReactiveUI;
using ReactiveUI.Primitives.Signals;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Dialogs;

/// <summary>The confirm dialog: shows a <see cref="ConfirmRequest"/> and answers it.</summary>
[DebuggerDisplay("ConfirmViewModel: {Title}")]
public sealed partial class ConfirmViewModel : ReactiveObject, ILocalisationChildren
{
    /// <summary>The question and its retained interface resources.</summary>
    private readonly ConfirmRequest _request;

    /// <summary>Initializes a new instance of the <see cref="ConfirmViewModel"/> class.</summary>
    /// <param name="request">The question.</param>
    public ConfirmViewModel(ConfirmRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);
        _request = request;
        Answered = Signal.Merge(ConfirmCommand, CancelCommand);
    }

    /// <summary>Gets the short question.</summary>
    public string Title => _request.TitleMessage?.ToString() ?? _request.Title;

    /// <summary>Gets what will happen and how to undo it.</summary>
    public string Message => _request.ContentMessage?.ToString() ?? _request.Message;

    /// <summary>Gets the text of the button that goes ahead.</summary>
    public string ConfirmText => _request.ConfirmMessage?.ToString() ?? _request.ConfirmText;

    /// <summary>Gets the answer: <see langword="true"/> to go ahead.</summary>
    public IObservable<bool> Answered { get; }

    /// <inheritdoc/>
    void ILocalisationChildren.Refresh(HashSet<object> visited)
    {
        this.RaisePropertyChanged(nameof(Title));
        this.RaisePropertyChanged(nameof(Message));
        this.RaisePropertyChanged(nameof(ConfirmText));
    }

    /// <summary>Goes ahead with the action.</summary>
    /// <returns>Always <see langword="true"/>.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Confirm() => true;

    /// <summary>Keeps things as they are.</summary>
    /// <returns>Always <see langword="false"/>.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Cancel() => false;
}
