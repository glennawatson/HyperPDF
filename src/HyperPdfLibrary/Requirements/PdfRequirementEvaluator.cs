// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdfLibrary.Requirements;

/// <summary>
/// Checks requirements against a viewer's capabilities and adds up the penalties of the unmet ones (ISO 32000-2 §12.11.3
/// and §12.11.6). Nothing a document holds is run: handlers are data, and the checks use only the capability table.
/// </summary>
internal static class PdfRequirementEvaluator
{
    /// <summary>The total penalty at which a document's author intent cannot be produced (§12.11.3).</summary>
    internal const int CriticalPenalty = 100;

    /// <summary>The requirement type that asks for encryption settings.</summary>
    private const string EncryptionType = "Encryption";

    /// <summary>The security handler that protects with passwords.</summary>
    private const string StandardFilter = "Standard";

    /// <summary>The security handler that protects with certificates.</summary>
    private const string PublicKeyFilter = "Adobe.PubSec";

    /// <summary>The lowest encryption algorithm number the viewer reads.</summary>
    private const int MinEncryptionVersion = 1;

    /// <summary>The highest encryption algorithm number the viewer reads.</summary>
    private const int MaxEncryptionVersion = 5;

    /// <summary>Checks a list of requirements.</summary>
    /// <param name="list">The requirements.</param>
    /// <param name="capabilities">What the viewer provides.</param>
    /// <returns>A result for each requirement, the total penalty and what it allows.</returns>
    internal static PdfRequirementAssessment Evaluate(PdfRequirementList list, PdfRequirementCapabilities capabilities)
    {
        if (list.Requirements.Count == 0)
        {
            return PdfRequirementAssessment.Empty;
        }

        var results = new PdfRequirementResult[list.Requirements.Count];
        var total = 0;
        var unmet = 0;
        for (var i = 0; i < results.Length; i++)
        {
            results[i] = Check(list.Requirements[i], capabilities);
            total += results[i].AppliedPenalty;
            unmet += results[i].IsMet ? 0 : 1;
        }

        var decision = Decide(unmet, total);
        return new(results, total, decision, PdfRequirementText.Summarize(results, decision));
    }

    /// <summary>Decides what the unmet requirements allow.</summary>
    /// <param name="unmet">The number of unmet requirements.</param>
    /// <param name="total">The sum of their penalties.</param>
    /// <returns>The decision.</returns>
    private static PdfRequirementDecision Decide(int unmet, int total)
    {
        if (unmet == 0)
        {
            return PdfRequirementDecision.Satisfied;
        }

        return total >= CriticalPenalty ? PdfRequirementDecision.Critical : PdfRequirementDecision.Fallback;
    }

    /// <summary>Checks one requirement.</summary>
    /// <param name="requirement">The requirement.</param>
    /// <param name="capabilities">What the viewer provides.</param>
    /// <returns>The result.</returns>
    private static PdfRequirementResult Check(PdfRequirement requirement, PdfRequirementCapabilities capabilities)
    {
        if (capabilities.Find(requirement.Type) is not { } capability)
        {
            return new(requirement, PdfRequirementOutcome.UnknownType, requirement.Type, PdfRequirementText.Unknown(requirement), false);
        }

        var outcome = OutcomeOf(requirement, capability);
        return new(requirement, outcome, capability.Title, PdfRequirementText.Describe(requirement, capability, outcome), true);
    }

    /// <summary>Works out whether a viewer meets a requirement of a type it knows.</summary>
    /// <param name="requirement">The requirement.</param>
    /// <param name="capability">The viewer's row for the type.</param>
    /// <returns>The outcome.</returns>
    private static PdfRequirementOutcome OutcomeOf(PdfRequirement requirement, PdfViewerCapability capability)
    {
        if (capability.Support == PdfRequirementSupport.Unsupported)
        {
            return PdfRequirementOutcome.NotSupported;
        }

        if (!VersionIsMet(requirement.Version, capability))
        {
            return PdfRequirementOutcome.VersionNotSupported;
        }

        if (string.Equals(requirement.Type, EncryptionType, StringComparison.Ordinal) && !EncryptionIsMet(requirement.Encryption))
        {
            return PdfRequirementOutcome.NotSupported;
        }

        return capability.Support == PdfRequirementSupport.Partial ? PdfRequirementOutcome.PartlySupported : PdfRequirementOutcome.Met;
    }

    /// <summary>Determines whether the viewer provides at least the version a requirement asks for.</summary>
    /// <param name="version">The requirement's <c>/V</c>, or <see langword="null"/>.</param>
    /// <param name="capability">The viewer's row for the type.</param>
    /// <returns><see langword="true"/> when there is no version to meet, the entry is unreadable, or the viewer's version is at least as high.</returns>
    private static bool VersionIsMet(PdfRequirementVersion? version, PdfViewerCapability capability)
    {
        // An unreadable version is read leniently, as if absent. A vendor extension level is not one the viewer knows.
        if (version is not { IsValid: true })
        {
            return true;
        }

        return !version.IsExtension && PdfRequirementReader.CompareVersions(version.Text, capability.SupportedVersion) <= 0;
    }

    /// <summary>Determines whether the viewer can read files protected the way an encryption requirement asks.</summary>
    /// <param name="encryption">The settings, or <see langword="null"/>.</param>
    /// <returns><see langword="true"/> for a password or certificate handler with an algorithm number the viewer reads.</returns>
    private static bool EncryptionIsMet(PdfRequirementEncryption? encryption) =>
        encryption is { Filter: StandardFilter or PublicKeyFilter, Version: >= MinEncryptionVersion and <= MaxEncryptionVersion };
}
