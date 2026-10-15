// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Fonts.Embedding;

/// <summary>
/// Lends one cleared list of <typeparamref name="T"/> at a time to the text shaping and planning steps, so showing text
/// does not make a new list and grow it for every call. A caller that finds the list taken makes its own.
/// </summary>
/// <typeparam name="T">The element type.</typeparam>
internal static class ScratchList<T>
{
    /// <summary>The largest list kept; a list that grew past it is left to the collector.</summary>
    private const int MaximumKept = 4096;

    /// <summary>Takes a cleared list.</summary>
    /// <param name="idle">The owning document's idle list slot.</param>
    /// <param name="capacity">The capacity wanted when a new list has to be made.</param>
    /// <returns>The list, which the caller returns with <see cref="Return"/>.</returns>
    internal static List<T> Rent(ref List<T>? idle, int capacity) => Interlocked.Exchange(ref idle, null) ?? [with(capacity)];

    /// <summary>Gives a list back.</summary>
    /// <param name="idle">The owning document's idle list slot.</param>
    /// <param name="list">The list taken with <see cref="Rent"/>.</param>
    internal static void Return(ref List<T>? idle, List<T> list)
    {
        if (list.Capacity > MaximumKept)
        {
            return;
        }

        list.Clear();
        Volatile.Write(ref idle, list);
    }
}
