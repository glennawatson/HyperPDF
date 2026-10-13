// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Core.Forms;

/// <summary>Applies an FDF or XFDF file to the open document as one undoable edit. The host gets it only after the user agrees to the import.</summary>
public interface IFormDataImporter
{
    /// <summary>Reads FDF or XFDF bytes and applies their field values and annotations to the document.</summary>
    /// <param name="data">The file's bytes, which the engine bounds and reads as untrusted.</param>
    /// <returns>What changed; <see cref="FormDataImportResult.Succeeded"/> is <see langword="false"/> and the document is unchanged when the file is not valid FDF or XFDF.</returns>
    FormDataImportResult Import(ReadOnlyMemory<byte> data);
}
