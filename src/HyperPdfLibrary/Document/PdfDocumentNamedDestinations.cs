// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Document;

/// <summary>Reads and edits the document's named destinations (ISO 32000-2 12.3.2.3), kept in the <c>/Names /Dests</c> name tree.</summary>
public static class PdfDocumentNamedDestinations
{
    /// <summary>Gets the named destinations of the name tree, in name order.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The named destinations.</returns>
    /// <exception cref="ArgumentNullException"><paramref name="document"/> is <see langword="null"/>.</exception>
    public static IReadOnlyList<PdfNamedDestination> GetNamedDestinations(PdfDocument document)
    {
        ArgumentNullException.ThrowIfNull(document);
        var entries = PdfDocumentNameTrees.GetEntries(document, PdfNameTreeKind.Destinations);
        var result = new PdfNamedDestination[entries.Count];
        for (var i = 0; i < result.Length; i++)
        {
            result[i] = new(entries[i].Name, PdfDocumentNavigation.ResolveDestination(document, entries[i].Value));
        }

        return result;
    }

    /// <summary>Adds a named destination, or moves an existing name to a new place.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The name.</param>
    /// <param name="destination">Where the name leads.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentOutOfRangeException">The destination's page is not a page of the document.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    public static void SetNamedDestination(PdfDocument document, string name, PdfExplicitDestination destination)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        PdfDocumentNameTrees.SetEntry(document, PdfNameTreeKind.Destinations, name, PdfValue.FromArray(PdfDestinationWriter.Create(document, destination)));
    }

    /// <summary>Adds a named destination, or moves an existing name to a new place.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The name.</param>
    /// <param name="destination">Where the name leads.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns>A task that completes when the destination is added.</returns>
    public static ValueTask SetNamedDestinationAsync(PdfDocument document, string name, PdfExplicitDestination destination, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(destination);
        return PdfDocumentNameTrees.SetEntryAsync(document, PdfNameTreeKind.Destinations, name, PdfValue.FromArray(PdfDestinationWriter.Create(document, destination)), cancellationToken);
    }

    /// <summary>Removes a named destination. Bookmarks and links that name it lead nowhere afterwards.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The name.</param>
    /// <returns><see langword="true"/> when the name was there.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="InvalidOperationException">The document is certified and does not allow the change.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static bool RemoveNamedDestination(PdfDocument document, string name) => PdfDocumentNameTrees.RemoveEntry(document, PdfNameTreeKind.Destinations, name);

    /// <summary>Removes a named destination. Bookmarks and links that name it lead nowhere afterwards.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The name.</param>
    /// <param name="cancellationToken">Cancels the loading; the document is unchanged when it is cancelled.</param>
    /// <returns><see langword="true"/> when the name was there.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static ValueTask<bool> RemoveNamedDestinationAsync(PdfDocument document, string name, CancellationToken cancellationToken) =>
        PdfDocumentNameTrees.RemoveEntryAsync(document, PdfNameTreeKind.Destinations, name, cancellationToken);
}
