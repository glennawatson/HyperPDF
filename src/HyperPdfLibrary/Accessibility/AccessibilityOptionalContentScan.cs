// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Checks configuration names and automatic states required by PDF/UA-1 7.10 and PDF/UA-2 8.7.</summary>
internal static class AccessibilityOptionalContentScan
{
    /// <summary>Reports defects without changing the source configurations or their visibility behaviour.</summary>
    /// <param name="catalog">The document catalog.</param>
    /// <param name="part">The claimed PDF/UA part.</param>
    /// <param name="findings">The operation's finding storage.</param>
    /// <param name="cancellationToken">Cancels configuration traversal.</param>
    internal static void Run(PdfDictionary catalog, int? part, AccessibilityFindings findings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (part is not (PdfUaClaim.Part1 or PdfUaClaim.Part2) || catalog.GetDictionary(KnownName.OCProperties) is not { } properties)
        {
            return;
        }

        var configurations = properties.GetArray(KnownName.Configs);
        var requireName = HasConfiguration(configurations, cancellationToken);
        Check(properties.GetDictionary(KnownName.D), requireName, findings);
        if (configurations is null)
        {
            return;
        }

        for (var index = 0; index < configurations.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            Check(configurations.GetDictionary(index), requireName, findings);
        }
    }

    /// <summary>Tests the conditional name requirement against actual configuration dictionaries, not just array length.</summary>
    /// <param name="configurations">The alternate configurations, when present.</param>
    /// <param name="cancellationToken">Cancels traversal of damaged array entries.</param>
    /// <returns>True when the array contains a configuration dictionary.</returns>
    private static bool HasConfiguration(PdfArray? configurations, CancellationToken cancellationToken)
    {
        if (configurations is null)
        {
            return false;
        }

        for (var index = 0; index < configurations.Count; index++)
        {
            cancellationToken.ThrowIfCancellationRequested();
            if (configurations.GetDictionary(index) is not null)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>Counts each unmet rule on a configuration, ignoring non-dictionaries in damaged input.</summary>
    /// <param name="configuration">The configuration dictionary.</param>
    /// <param name="requireName">Whether alternate configurations make a nonempty name mandatory.</param>
    /// <param name="findings">Receives document-level findings.</param>
    private static void Check(PdfDictionary? configuration, bool requireName, AccessibilityFindings findings)
    {
        if (configuration is null)
        {
            return;
        }

        var name = configuration.Get(KnownName.Name);
        if (requireName && (name.Kind != PdfKind.String || PdfText.GetDecodedLength(name.AsStringBytes()) == 0))
        {
            AccessibilityFindingCollection.Add(findings, PdfAccessibilityCode.OptionalContentNameMissing);
        }

        // ISO 32000 null dictionary values are equivalent to omitted entries.
        if (configuration.Get(KnownName.AS).Kind != PdfKind.Null)
        {
            AccessibilityFindingCollection.Add(findings, PdfAccessibilityCode.OptionalContentAutomaticState);
        }
    }
}
