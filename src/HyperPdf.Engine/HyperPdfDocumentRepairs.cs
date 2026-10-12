// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdfLibrary.Document;
namespace HyperPdf.Engine;

/// <summary>Implements DocumentRepairs over the document's owned state.</summary>
internal static class HyperPdfDocumentRepairs
{
    /// <summary>Gets WasRepaired.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The current value.</returns>
    internal static bool GetWasRepaired(HyperPdfDocument self) => !self.IsDisposed && PdfDocumentCheck.WasRepaired(self.Document);

    /// <summary>Gets the repairs made so far, in plain words, each once.</summary>
    /// <param name="self">The owning document.</param>
    /// <returns>The repairs; empty when the document was not repaired.</returns>
    internal static IReadOnlyList<RepairNote> GetRepairs(HyperPdfDocument self)
    {
        if (self.IsDisposed)
        {
            return [];
        }

        var found = PdfDocumentCheck.GetRepairs(self.Document);
        var notes = new RepairNote[found.Length];
        for (var i = 0; i < found.Length; i++)
        {
            notes[i] = new(HyperPdfRepairs.Describe(found[i].Code), found[i].ObjectNumber, found[i].Offset);
        }

        return notes;
    }
}
