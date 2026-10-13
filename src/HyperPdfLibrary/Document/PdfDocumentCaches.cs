// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdfLibrary.Document;

/// <summary>
/// Publishes values a reader computed from the document's objects into the document's caches. A reader reads the revision
/// before it computes; an edit changes the revision before it clears the caches. So a value computed from objects an edit
/// replaced is never left in a cache.
/// </summary>
internal static class PdfDocumentCaches
{
    /// <summary>Gets the revision of the document's objects, to read before computing a value to publish.</summary>
    /// <param name="document">The document.</param>
    /// <returns>The revision.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal static int Revision(PdfDocument document) => Volatile.Read(ref document.State.ObjectsRevision);

    /// <summary>
    /// Stores a value in a cache slot, and takes it out again when an edit ran since the revision was read. An edit that runs
    /// after the check clears the slot itself.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="slot">The cache slot.</param>
    /// <param name="value">The value.</param>
    /// <param name="revision">The revision read before the value was computed.</param>
    /// <returns>The value stored, which the caller uses whether or not it stayed in the slot.</returns>
    internal static T Publish<T>(PdfDocument document, ref T? slot, T value, int revision)
        where T : class
    {
        Volatile.Write(ref slot, value);
        if (Volatile.Read(ref document.State.ObjectsRevision) != revision)
        {
            _ = Interlocked.CompareExchange(ref slot, null, value);
        }

        return value;
    }

    /// <summary>
    /// Keeps the first value stored in a cache slot, so threads that computed the same value share one, and takes the kept
    /// value out when an edit ran since the revision was read.
    /// </summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="slot">The cache slot.</param>
    /// <param name="value">The value.</param>
    /// <param name="revision">The revision read before the value was computed.</param>
    /// <returns>The value that is in the slot: the earlier one when another thread stored first, otherwise <paramref name="value"/>.</returns>
    internal static T PublishFirst<T>(PdfDocument document, ref T? slot, T value, int revision)
        where T : class
    {
        var winner = Interlocked.CompareExchange(ref slot, value, null) ?? value;
        if (Volatile.Read(ref document.State.ObjectsRevision) != revision)
        {
            _ = Interlocked.CompareExchange(ref slot, null, winner);
        }

        return winner;
    }

    /// <summary>Gets the array that caches a value for each page, made for the current page count.</summary>
    /// <typeparam name="T">The type of the cached value.</typeparam>
    /// <param name="document">The document.</param>
    /// <param name="slot">The cache slot.</param>
    /// <param name="revision">The revision read before the page count was.</param>
    /// <returns>The array. It is private to the caller, and not cached, when an edit ran since the revision was read.</returns>
    internal static T?[] PerPage<T>(PdfDocument document, ref T?[]? slot, int revision)
        where T : class =>
        Volatile.Read(ref slot) ?? PdfDocumentCaches.PublishFirst(document, ref slot, new T?[document.PageCount], revision);
}
