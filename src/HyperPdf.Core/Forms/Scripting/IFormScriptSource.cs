// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Forms.Scripting;

/// <summary>Reads the scripts of a document's form fields. Implemented by documents with forms; safe from any thread.</summary>
public interface IFormScriptSource : IDocumentFeature
{
    /// <summary>Appends the scripts of the fields on a page that have any HyperPDF runs.</summary>
    /// <param name="pageIndex">The zero based page index.</param>
    /// <param name="output">The list receiving the scripts.</param>
    void GetScripts(int pageIndex, List<FieldScripts> output);
}
