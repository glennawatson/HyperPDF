// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using System.Runtime.CompilerServices;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Accessibility;

/// <summary>
/// Collects findings while the report is built. Every finding is counted, but each code keeps at most
/// <see cref="MaxFindingsPerCode"/> of them, so a huge file with one repeated fault cannot make a huge report.
/// </summary>
[DebuggerDisplay("AccessibilityFindings: {_findings.Count} findings")]
internal sealed class AccessibilityFindings
{
    /// <summary>The most findings kept for one code.</summary>
    internal const int MaxFindingsPerCode = 200;

    /// <summary>The page index of a finding about the whole document.</summary>
    internal const int DocumentLevel = -1;

    /// <summary>The kept findings, in the order found.</summary>
    private readonly List<PdfAccessibilityFinding> _findings = [];

    /// <summary>The number of times each code was found, indexed by code value.</summary>
    private readonly int[] _counts = new int[PdfAccessibilityMessages.CodeCount];

    /// <summary>Gets the kept findings.</summary>
    internal List<PdfAccessibilityFinding> Findings => _findings;

    /// <summary>Adds a finding about the whole document.</summary>
    /// <param name="code">The code.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Add(PdfAccessibilityCode code) => Add(code, DocumentLevel, default);

    /// <summary>Adds a finding about a page.</summary>
    /// <param name="code">The code.</param>
    /// <param name="pageIndex">The zero based page index.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void Add(PdfAccessibilityCode code, int pageIndex) => Add(code, pageIndex, default);

    /// <summary>Adds a finding about a structure element.</summary>
    /// <param name="code">The code.</param>
    /// <param name="pageIndex">The zero based page index, or -1.</param>
    /// <param name="elementId">The element's object id; not valid for a direct dictionary.</param>
    internal void Add(PdfAccessibilityCode code, int pageIndex, PdfObjectId elementId)
    {
        var index = (int)code;
        _counts[index]++;
        if (_counts[index] > MaxFindingsPerCode)
        {
            return;
        }

        _findings.Add(new(code, PdfAccessibilityMessages.For(code), pageIndex, elementId.IsValid ? elementId : null));
    }

    /// <summary>Gets the count of each code that was found, in code order.</summary>
    /// <returns>The counts.</returns>
    internal PdfAccessibilityCount[] GetCounts()
    {
        var length = 0;
        for (var i = 1; i < _counts.Length; i++)
        {
            length += _counts[i] > 0 ? 1 : 0;
        }

        var counts = new PdfAccessibilityCount[length];
        var next = 0;
        for (var i = 1; i < _counts.Length; i++)
        {
            if (_counts[i] == 0)
            {
                continue;
            }

            counts[next] = new((PdfAccessibilityCode)i, _counts[i]);
            next++;
        }

        return counts;
    }
}
