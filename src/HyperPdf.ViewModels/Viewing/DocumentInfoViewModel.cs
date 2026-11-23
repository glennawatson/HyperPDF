// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdf.Core.Documents;
using ReactiveUI;
using ReactiveUI.SourceGenerators;

namespace HyperPdf.ViewModels.Viewing;

/// <summary>The window that changes the document's title, author, subject and keywords.</summary>
[DebuggerDisplay("DocumentInfoViewModel: {Title}")]
public sealed partial class DocumentInfoViewModel : ReactiveObject
{
    /// <summary>Initializes a new instance of the <see cref="DocumentInfoViewModel"/> class.</summary>
    /// <param name="metadata">The details the document has now.</param>
    public DocumentInfoViewModel(DocumentMetadata metadata)
    {
        ArgumentNullException.ThrowIfNull(metadata);
        Title = metadata.Title ?? string.Empty;
        Author = metadata.Author ?? string.Empty;
        Subject = metadata.Subject ?? string.Empty;
        Keywords = metadata.Keywords ?? string.Empty;
        Answered = Signal.Merge(AcceptCommand, CancelCommand);
    }

    /// <summary>Gets the answer: whether the details were accepted.</summary>
    public IObservable<bool> Answered { get; }

    /// <summary>Gets or sets the title.</summary>
    [Reactive]
    public partial string Title { get; set; }

    /// <summary>Gets or sets who wrote the document.</summary>
    [Reactive]
    public partial string Author { get; set; }

    /// <summary>Gets or sets what the document is about.</summary>
    [Reactive]
    public partial string Subject { get; set; }

    /// <summary>Gets or sets the words to find the document by.</summary>
    [Reactive]
    public partial string Keywords { get; set; }

    /// <summary>Makes the edit the window's values describe, with the spaces at either end taken off.</summary>
    /// <returns>The edit.</returns>
    public DocumentInfoEdit ToEdit() => new(Title.Trim(), Author.Trim(), Subject.Trim(), Keywords.Trim());

    /// <summary>Closes the window without changing anything.</summary>
    /// <returns><see langword="false"/>.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Cancel() => false;

    /// <summary>Closes the window and keeps the details.</summary>
    /// <returns><see langword="true"/>.</returns>
    [ReactiveCommand]
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static bool Accept() => true;
}
