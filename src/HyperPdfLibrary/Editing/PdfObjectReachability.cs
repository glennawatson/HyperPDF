// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>Finds which indirect objects the document still reaches, so objects an edit left behind can be deleted.</summary>
internal static class PdfObjectReachability
{
    /// <summary>Checks cancellation after each 256 visited values.</summary>
    private const int CancellationCheckMask = 255;

    /// <summary>Lists the numbers of the objects reachable from the trailer's /Root and /Info, as the edit layer sees them.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="cancellationToken">Cancels the walk.</param>
    /// <returns>The object numbers.</returns>
    internal static HashSet<int> FromTrailer(PdfObjectStore store, CancellationToken cancellationToken)
    {
        var reached = new HashSet<int>();
        var pending = new Stack<PdfValue>();
        pending.Push(store.Trailer.GetRaw(KnownName.Root));
        pending.Push(store.Trailer.GetRaw(KnownName.Info));
        var visits = 0;
        while (pending.Count > 0)
        {
            visits++;
            if ((visits & CancellationCheckMask) == 0)
            {
                cancellationToken.ThrowIfCancellationRequested();
            }

            Visit(store, pending.Pop(), reached, pending);
        }

        return reached;
    }

    /// <summary>Handles one value: a new reference is followed, and the members of a container are queued.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="value">The value.</param>
    /// <param name="reached">The numbers reached so far.</param>
    /// <param name="pending">The values still to visit.</param>
    private static void Visit(PdfObjectStore store, PdfValue value, HashSet<int> reached, Stack<PdfValue> pending)
    {
        switch (value.Kind)
        {
            case PdfKind.Reference:
            {
                var id = value.AsReference();
                if (id.IsValid && reached.Add(id.Number))
                {
                    pending.Push(StoreReading.GetObject(store, id));
                }

                break;
            }

            case PdfKind.Array:
            {
                foreach (var item in value.AsArray()!.Items)
                {
                    pending.Push(item);
                }

                break;
            }

            case PdfKind.Dictionary or PdfKind.Stream:
            {
                var dictionary = value.AsDictionary()!;
                for (var i = 0; i < dictionary.Count; i++)
                {
                    pending.Push(dictionary.GetValueAt(i));
                }

                break;
            }

            default:
            {
                break;
            }
        }
    }
}
