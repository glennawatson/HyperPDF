// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.PdfUa;

/// <summary>Runs the repairs for the requirements a preflight found broken, in the order that lets each one read the result of the one before.</summary>
internal static class PdfUaRepairs
{
    /// <summary>The repairs, in the order they run: tags first, then what reads the tags, then the catalog entries.</summary>
    private static readonly PdfUaRepairStep[] Steps =
    [
        new([PdfUaRequirement.StructureReferences], static (document, transaction, _, token) => PdfUaStructureReferences.Repair(document, transaction, token)),
        new([PdfUaRequirement.AnnotationTagged], static (document, transaction, _, token) => PdfUaAnnotationTagging.Repair(document, transaction, token)),
        new([PdfUaRequirement.UnicodeMapping], static (document, transaction, _, token) => PdfUaToUnicodeRepair.Repair(document, transaction, token)),
        new([PdfUaRequirement.TabOrder], static (document, transaction, _, token) => PdfUaTabOrder.Repair(document, transaction, token)),
        new([PdfUaRequirement.Title], static (document, transaction, _, _) => PdfUaTitleRepair.Repair(document, transaction)),
        new(
            [PdfUaRequirement.Marked, PdfUaRequirement.DisplayDocTitle, PdfUaRequirement.Language],
            static (document, transaction, wanted, _) => PdfUaCatalogRepair.Repair(document, transaction, wanted)),
    ];

    /// <summary>Runs the repairs that the found requirements call for.</summary>
    /// <param name="store">The objects being written.</param>
    /// <param name="transaction">The edit the repairs are made in.</param>
    /// <param name="wanted">The requirements the preflight found broken.</param>
    /// <param name="cancellationToken">Stops the repairs.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static void Run(PdfObjectStore store, PdfEditTransaction transaction, HashSet<PdfUaRequirement> wanted, CancellationToken cancellationToken)
    {
        foreach (var step in Steps)
        {
            if (!step.IsWanted(wanted))
            {
                continue;
            }

            // Each repair reads a view made after the one before it, as a view keeps what it read.
            step.Run(PdfUaPreflight.OpenView(store), transaction, wanted, cancellationToken);
        }
    }

    /// <summary>One repair and the requirements that call for it.</summary>
    /// <param name="Triggers">The requirements that make the repair run.</param>
    /// <param name="Run">The repair.</param>
    private sealed record PdfUaRepairStep(PdfUaRequirement[] Triggers, Action<PdfDocument, PdfEditTransaction, HashSet<PdfUaRequirement>, CancellationToken> Run)
    {
        /// <summary>Determines whether any trigger was found broken.</summary>
        /// <param name="wanted">The requirements found broken.</param>
        /// <returns><see langword="true"/> when the repair should run.</returns>
        internal bool IsWanted(HashSet<PdfUaRequirement> wanted)
        {
            foreach (var trigger in Triggers)
            {
                if (wanted.Contains(trigger))
                {
                    return true;
                }
            }

            return false;
        }
    }
}
