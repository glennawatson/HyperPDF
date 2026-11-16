// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Forms;

/// <summary>
/// The application's part of running a form's actions: moving around, opening things, and anything that leaves the
/// document. A host that sends or imports data must ask the user first.
/// </summary>
public interface IFormActionHost
{
    /// <summary>Runs a named viewer action such as <c>NextPage</c>.</summary>
    /// <param name="name">The action's name.</param>
    /// <param name="sourcePage">The page the action came from, or -1.</param>
    /// <returns>A cold signal giving <see langword="true"/> once when the host ran it; unsubscribing cancels the call.</returns>
    IObservable<bool> RunNamed(string name, int sourcePage);

    /// <summary>Follows a link target such as a page, an address or another file.</summary>
    /// <param name="target">The target.</param>
    /// <returns>A cold signal giving <see langword="true"/> once when the host followed it; unsubscribing cancels the call.</returns>
    IObservable<bool> Open(LinkTarget target);

    /// <summary>Offers form data to send. The host sends it only if the user agrees.</summary>
    /// <param name="submission">The data.</param>
    /// <returns>A cold signal giving <see langword="true"/> once when the host sent it; unsubscribing cancels the call.</returns>
    IObservable<bool> Submit(FormSubmission submission);

    /// <summary>
    /// Offers to import form data from a file. The host imports it only if the user agrees, and reads the file the user
    /// chooses rather than the path the document names, which the document controls.
    /// </summary>
    /// <param name="file">The file as the document names it, or <see langword="null"/>. It is only shown to the user.</param>
    /// <param name="importer">Applies the chosen file's bytes to the document.</param>
    /// <returns>A cold signal giving <see langword="true"/> once when the host imported it; unsubscribing cancels the call.</returns>
    IObservable<bool> Import(string? file, IFormDataImporter importer);
}
