// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Collections.Immutable;
using System.Diagnostics;

namespace HyperPdfLibrary.Accessibility;

/// <summary>What a file declares about Well-Tagged PDF and where it falls short.</summary>
/// <param name="DeclaresReuse">Whether the XMP holds the reuse declaration.</param>
/// <param name="DeclaresAccessibility">Whether the XMP holds the accessibility declaration.</param>
/// <param name="Findings">Every shortfall found, in document order.</param>
[DebuggerDisplay("PdfWtpdfReport: reuse {DeclaresReuse}, accessibility {DeclaresAccessibility}, {Findings.Length} findings")]
public sealed record PdfWtpdfReport(bool DeclaresReuse, bool DeclaresAccessibility, ImmutableArray<PdfWtpdfFinding> Findings)
{
    /// <summary>Counts the findings that stop a level from holding.</summary>
    /// <param name="profile">The level.</param>
    /// <returns>The number of findings that apply to it.</returns>
    public int CountFor(PdfWtpdfProfile profile)
    {
        var count = 0;
        foreach (var finding in Findings)
        {
            if (finding.Profile <= profile)
            {
                count++;
            }
        }

        return count;
    }
}
