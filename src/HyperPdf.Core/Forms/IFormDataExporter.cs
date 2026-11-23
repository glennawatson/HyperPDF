// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;

namespace HyperPdf.Core.Forms;

/// <summary>Writes the answers of the document's form to a file another program can read back.</summary>
public interface IFormDataExporter : IDocumentFeature
{
    /// <summary>Writes the form's field values as FDF or XFDF.</summary>
    /// <param name="format">The kind of file.</param>
    /// <returns>The file's bytes.</returns>
    byte[] Export(FormDataFormat format);
}
