// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Frozen;
using System.Runtime.InteropServices;
using HyperPdf.Scripts;

namespace HyperPdfLibrary.Tests.Graphics.Jpx;

/// <summary>
/// The HT cleanup VLC codewords by context, significance pattern and u-offset, read straight from the CxtVLC rows of
/// T.814 Annex C (the encoding tables of clause 8 derive from the same rows). The decoder's lookup tables are not used.
/// </summary>
internal static class JpxTestHtCodebook
{
    /// <summary>The shift of the table in a key.</summary>
    private const int TableKeyShift = 8;

    /// <summary>The shift of the context in a key.</summary>
    private const int ContextKeyShift = 5;

    /// <summary>The codewords of each key.</summary>
    private static readonly FrozenDictionary<int, List<JpxTestHtCode>> Codes = Build().ToFrozenDictionary();

    /// <summary>Gets the codewords for a quad.</summary>
    /// <param name="firstRow">Whether the quad is in the first row.</param>
    /// <param name="context">The context.</param>
    /// <param name="rho">The significance pattern.</param>
    /// <param name="offset">Whether the residual is above zero.</param>
    /// <returns>The codewords, shortest first; empty when none.</returns>
    internal static List<JpxTestHtCode> Find(bool firstRow, int context, int rho, bool offset) =>
        Codes.TryGetValue(Key(firstRow ? 0 : 1, context, rho, offset ? 1 : 0), out var list) ? list : [];

    /// <summary>Makes a lookup key.</summary>
    /// <param name="table">0 for the first row, 1 for later rows.</param>
    /// <param name="context">The context.</param>
    /// <param name="rho">The significance pattern.</param>
    /// <param name="offset">The u-offset bit.</param>
    /// <returns>The key.</returns>
    private static int Key(int table, int context, int rho, int offset) =>
        (table << TableKeyShift) | (context << ContextKeyShift) | (rho << 1) | offset;

    /// <summary>Groups the rows of both tables by key.</summary>
    /// <returns>The codewords of each key, shortest first.</returns>
    private static Dictionary<int, List<JpxTestHtCode>> Build()
    {
        var codes = new Dictionary<int, List<JpxTestHtCode>>();
        foreach (var table in JpxTestHtRows.Load())
        {
            foreach (var row in table)
            {
                var key = Key(row.Table, row.Context, row.Rho, row.Offset);
                ref var list = ref CollectionsMarshal.GetValueRefOrAddDefault(codes, key, out _);
                list ??= [];
                list.Add(new(row.Word, row.Length, row.Known, row.Ones));
            }
        }

        foreach (var list in codes.Values)
        {
            list.Sort(static (a, b) => a.Length != b.Length ? a.Length.CompareTo(b.Length) : a.Bits.CompareTo(b.Bits));
        }

        return codes;
    }
}
