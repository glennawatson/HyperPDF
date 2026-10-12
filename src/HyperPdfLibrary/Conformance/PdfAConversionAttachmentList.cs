// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Conformance;

/// <summary>The file specifications in a document that embed a file.</summary>
[DebuggerDisplay("PdfAConversionAttachmentList: {Count} files")]
internal sealed class PdfAConversionAttachmentList
{
    /// <summary>Gets the object numbers of the indirect file specifications.</summary>
    internal List<int> References { get; } = [];

    /// <summary>Gets or sets the file specifications held directly inside another object.</summary>
    internal int Direct { get; set; }

    /// <summary>Gets the number of file specifications found.</summary>
    internal int Count => References.Count + Direct;

    /// <summary>Finds the file specifications the document reaches.</summary>
    /// <param name="session">The conversion.</param>
    /// <returns>The list.</returns>
    internal static PdfAConversionAttachmentList Find(PdfAConversionSession session)
    {
        var list = new PdfAConversionAttachmentList();
        var graph = PdfObjectGraph.Collect(session.Store, false, session.CancellationToken);
        for (var number = 1; number <= graph.Count; number++)
        {
            var value = graph.GetValue(number);
            if (value.Kind == PdfKind.Dictionary && value.AsDictionary()!.ContainsKey(KnownName.EF))
            {
                list.References.Add(graph.GetOldNumber(number));
                CountNested(value.AsDictionary()!, list, 0);
                continue;
            }

            CountNested(value.AsDictionary(), list, 0);
            CountNested(value.AsArray(), list, 0);
        }

        return list;
    }

    /// <summary>Counts the file specifications nested directly inside a dictionary.</summary>
    /// <param name="dictionary">The dictionary, or <see langword="null"/>.</param>
    /// <param name="list">The list.</param>
    /// <param name="depth">The nesting depth.</param>
    private static void CountNested(PdfDictionary? dictionary, PdfAConversionAttachmentList list, int depth)
    {
        for (var i = 0; dictionary is not null && depth < PdfLimits.MaxNesting && i < dictionary.Count; i++)
        {
            CountValue(dictionary.GetValueAt(i), list, depth + 1);
        }
    }

    /// <summary>Counts the file specifications nested directly inside an array.</summary>
    /// <param name="array">The array, or <see langword="null"/>.</param>
    /// <param name="list">The list.</param>
    /// <param name="depth">The nesting depth.</param>
    private static void CountNested(PdfArray? array, PdfAConversionAttachmentList list, int depth)
    {
        for (var i = 0; array is not null && depth < PdfLimits.MaxNesting && i < array.Count; i++)
        {
            CountValue(array.GetRaw(i), list, depth + 1);
        }
    }

    /// <summary>Counts a direct value that is, or holds, a file specification.</summary>
    /// <param name="value">The value.</param>
    /// <param name="list">The list.</param>
    /// <param name="depth">The nesting depth.</param>
    private static void CountValue(PdfValue value, PdfAConversionAttachmentList list, int depth)
    {
        if (value.Kind == PdfKind.Dictionary)
        {
            list.Direct += value.AsDictionary()!.ContainsKey(KnownName.EF) ? 1 : 0;
            CountNested(value.AsDictionary(), list, depth);
        }
        else if (value.Kind == PdfKind.Array)
        {
            CountNested(value.AsArray(), list, depth);
        }
    }
}
