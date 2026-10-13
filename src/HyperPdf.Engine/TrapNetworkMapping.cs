// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Writing;

namespace HyperPdf.Engine;

/// <summary>Passes what a save or page copy did with trap networks on to the app's reports.</summary>
internal static class TrapNetworkMapping
{
    /// <summary>Copies the warnings the user should see from a save: every change other than a fix that kept the network.</summary>
    /// <param name="writer">The library writer's report.</param>
    /// <param name="caller">The app's report, or <see langword="null"/>.</param>
    internal static void AddWarnings(PdfSaveReport writer, DocumentSaveReport? caller)
    {
        if (caller is null)
        {
            return;
        }

        caller.TrapNetworkWarnings.AddRange(Warnings(writer.TrapNetworks));
    }

    /// <summary>Gets the warnings the user should see: every change other than a fix that kept the network.</summary>
    /// <param name="changes">The library's changes.</param>
    /// <returns>The messages.</returns>
    internal static string[] Warnings(IReadOnlyList<PdfTrapNetworkChange> changes)
    {
        var warnings = new List<string>(changes.Count);
        foreach (var change in changes)
        {
            if (change.Action != PdfTrapNetworkAction.Repaired)
            {
                warnings.Add(change.Message);
            }
        }

        return [.. warnings];
    }
}
