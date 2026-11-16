// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using ReactiveUI.Primitives.Signals;

namespace HyperPdf.Core.Documents;

/// <summary>Opens documents of one or more formats.</summary>
public interface IDocumentEngine
{
    /// <summary>Gets the engine display name.</summary>
    string Name { get; }

    /// <summary>Determines whether the engine handles the file, based on its name.</summary>
    /// <param name="path">The file path.</param>
    /// <returns><see langword="true"/> when the engine can open the file.</returns>
    bool CanOpen(string path);

    /// <summary>Opens a document.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="password">The password, if the document is encrypted.</param>
    /// <returns>The opened document.</returns>
    /// <exception cref="DocumentOpenException">Thrown when the document cannot be opened.</exception>
    IDocument Open(string path, string? password);

    /// <summary>Opens a document with cancellable I/O when the engine supports it.</summary>
    /// <param name="path">The file path.</param>
    /// <param name="password">The password, if the document is encrypted.</param>
    /// <returns>
    /// A cold signal giving the opened document once; unsubscribing cancels the open. It fails with
    /// <see cref="DocumentOpenException"/> when the document cannot be opened.
    /// </returns>
    IObservable<IDocument> Load(string path, string? password) => Signal.Defer(() => Signal.Return(Open(path, password)));
}
