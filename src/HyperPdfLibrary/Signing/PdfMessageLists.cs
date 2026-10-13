// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Signing;

/// <summary>
/// Lends the lists that collect the reasons and notes while a signature is planned. Most signatures have none to report, so the
/// lists are reused and only a plan that has messages keeps a copy of them.
/// </summary>
internal static class PdfMessageLists
{
    /// <summary>The idle lists the pool holds: a plan uses two at once.</summary>
    private const int SlotCount = 4;

    /// <summary>The idle lists.</summary>
    private static readonly List<string>?[] Slots = new List<string>?[SlotCount];

    /// <summary>Takes an empty list.</summary>
    /// <returns>The list; give it back with <see cref="Return"/>.</returns>
    internal static List<string> Rent()
    {
        for (var i = 0; i < Slots.Length; i++)
        {
            if (Interlocked.Exchange(ref Slots[i], null) is { } list)
            {
                return list;
            }
        }

        return [];
    }

    /// <summary>Copies a list's messages into something the caller may keep.</summary>
    /// <param name="list">The list.</param>
    /// <returns>The shared empty array when the list is empty, otherwise a copy.</returns>
    internal static IReadOnlyList<string> Snapshot(List<string> list) => list.Count == 0 ? [] : list.ToArray();

    /// <summary>Gives a list back after it was copied.</summary>
    /// <param name="list">The list; it is cleared.</param>
    internal static void Return(List<string> list)
    {
        list.Clear();
        for (var i = 0; i < Slots.Length; i++)
        {
            if (Interlocked.CompareExchange(ref Slots[i], list, null) is null)
            {
                return;
            }
        }
    }
}
