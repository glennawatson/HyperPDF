// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Forms;

/// <summary>
/// Reads and fills a document's interactive form. Changes go through the PDF's own form logic, so appearances and
/// calculated fields update as they would in other readers. Safe to call from any thread.
/// </summary>
public interface IFormFiller : IDocumentFeature
{
    /// <summary>Gets a value indicating whether the document has a fillable form.</summary>
    bool HasForm { get; }

    /// <summary>Appends the form fields on a page.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="output">The list receiving the fields.</param>
    void GetFields(int pageIndex, List<FormField> output);

    /// <summary>Replaces the text of a text field.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The widget index.</param>
    /// <param name="text">The new text.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    bool SetText(int pageIndex, int index, string text);

    /// <summary>Turns a check box or radio button on or off.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The widget index.</param>
    /// <param name="isChecked">The new state.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    bool SetChecked(int pageIndex, int index, bool isChecked);

    /// <summary>Selects a choice of a combo or list box.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="index">The widget index.</param>
    /// <param name="option">The option index.</param>
    /// <returns><see langword="true"/> when changed.</returns>
    bool SelectOption(int pageIndex, int index, int option);
}
