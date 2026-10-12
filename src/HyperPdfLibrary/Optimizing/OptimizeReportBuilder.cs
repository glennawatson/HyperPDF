// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Conformance;

namespace HyperPdfLibrary.Optimizing;

/// <summary>Collects what an optimisation run does, then builds the <see cref="PdfOptimizeReport"/>. Used by one thread.</summary>
[DebuggerDisplay("OptimizeReportBuilder: {_actions.Count} actions")]
internal sealed class OptimizeReportBuilder
{
    /// <summary>The number of categories.</summary>
    private const int CategoryCount = (int)PdfOptimizeCategory.Safety + 1;

    /// <summary>The objects each category changed.</summary>
    private readonly int[] _counts = new int[CategoryCount];

    /// <summary>The stored bytes of those objects before.</summary>
    private readonly long[] _before = new long[CategoryCount];

    /// <summary>The stored bytes of those objects after.</summary>
    private readonly long[] _after = new long[CategoryCount];

    /// <summary>The changes made.</summary>
    private readonly List<PdfOptimizeAction> _actions = [];

    /// <summary>The warnings.</summary>
    private readonly List<string> _warnings = [];

    /// <summary>What was left alone.</summary>
    private readonly List<PdfOptimizeSkip> _skipped = [];

    /// <summary>Gets or sets a value indicating whether a structure tree was inferred.</summary>
    internal bool TagsInferred { get; set; }

    /// <summary>Gets or sets the number of inferred figures without alternative text.</summary>
    internal int FiguresNeedingAltText { get; set; }

    /// <summary>Gets or sets the PDF/A part claimed.</summary>
    internal int PdfAPart { get; set; }

    /// <summary>Gets or sets the check of the written file's PDF/A claim.</summary>
    internal PdfAClaimCheck? PdfAClaim { get; set; }

    /// <summary>Gets or sets a value indicating whether the document was signed.</summary>
    internal bool WasSigned { get; set; }

    /// <summary>Gets or sets a value indicating whether the output is encrypted.</summary>
    internal bool IsEncrypted { get; set; }

    /// <summary>Records a change and its effect on stored size.</summary>
    /// <param name="category">The kind of change.</param>
    /// <param name="objectNumber">The source object, or zero.</param>
    /// <param name="description">What was done.</param>
    /// <param name="before">The stored bytes before.</param>
    /// <param name="after">The stored bytes after.</param>
    internal void Changed(PdfOptimizeCategory category, int objectNumber, string description, long before, long after)
    {
        Measure(category, before, after);
        _actions.Add(new(category, objectNumber, description, before, after));
    }

    /// <summary>Counts a change in a category's saving without listing it as an action, for bulk changes.</summary>
    /// <param name="category">The kind of change.</param>
    /// <param name="before">The stored bytes before.</param>
    /// <param name="after">The stored bytes after.</param>
    internal void Measure(PdfOptimizeCategory category, long before, long after)
    {
        _counts[(int)category]++;
        _before[(int)category] += before;
        _after[(int)category] += after;
    }

    /// <summary>Lists a change that has no size to measure.</summary>
    /// <param name="category">The kind of change.</param>
    /// <param name="objectNumber">The source object, or zero.</param>
    /// <param name="description">What was done.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Noted(PdfOptimizeCategory category, int objectNumber, string description) =>
        _actions.Add(new(category, objectNumber, description, 0, 0));

    /// <summary>Records something left alone.</summary>
    /// <param name="category">The kind of optimisation skipped.</param>
    /// <param name="objectNumber">The source object, or zero.</param>
    /// <param name="reason">Why.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Skip(PdfOptimizeCategory category, int objectNumber, string reason) => _skipped.Add(new(category, objectNumber, reason));

    /// <summary>Records a warning.</summary>
    /// <param name="warning">The warning.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Warn(string warning) => _warnings.Add(warning);

    /// <summary>Builds the report.</summary>
    /// <param name="bytesBefore">The source size.</param>
    /// <param name="bytesAfter">The bytes written.</param>
    /// <param name="mode">How the file was written.</param>
    /// <returns>The report.</returns>
    internal PdfOptimizeReport Build(long bytesBefore, long bytesAfter, PdfOptimizeMode mode)
    {
        var savings = new List<PdfOptimizeSaving>(CategoryCount);
        for (var i = 0; i < CategoryCount; i++)
        {
            if (_counts[i] > 0)
            {
                savings.Add(new((PdfOptimizeCategory)i, _counts[i], _before[i], _after[i]));
            }
        }

        return new(bytesBefore, bytesAfter, mode, savings, [.. _actions], [.. _warnings], [.. _skipped])
        {
            TagsInferred = TagsInferred,
            FiguresNeedingAltText = FiguresNeedingAltText,
            PdfAPart = PdfAPart,
            PdfAClaim = PdfAClaim,
            WasSigned = WasSigned,
            IsEncrypted = IsEncrypted,
        };
    }
}
