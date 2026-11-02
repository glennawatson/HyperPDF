// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// The PDF/X-4 rules about what the file holds rather than how it is laid out: embedded fonts, filters, scripts, embedded
/// and external content, transfer curves and alternate images.
/// </summary>
internal static class PdfXContentRules
{
    /// <summary>Where the self-contained file rules are stated.</summary>
    private const string SelfContained = "ISO 15930-7; PDF/X in a Nutshell, core principles: files must be complete";

    /// <summary>Where the prohibited feature rules are stated.</summary>
    private const string Prohibited = "ISO 15930-7; PDF/X in a Nutshell, core principles: prohibited features";

    /// <summary>The <c>/Default</c> name, the only transfer function name PDF/X accepts.</summary>
    private const string DefaultTransfer = "Default";

    /// <summary>The rules that turn one conformance scan count into a finding.</summary>
    private static readonly ObservationRule[] ObservationRules =
    [
        new(PdfConformanceFinding.NonEmbeddedFont, "Fonts", "Every font used is embedded.", "A font used by the pages has no embedded program", SelfContained),
        new(PdfConformanceFinding.LzwFilter, "LzwFilter", "No stream uses LZW.", "A stream uses the LZW filter", Prohibited),
        new(PdfConformanceFinding.JavaScript, "JavaScript", "The file holds no JavaScript.", "The file holds JavaScript", Prohibited),
        new(PdfConformanceFinding.EmbeddedFiles, "EmbeddedFiles", "The file embeds no files.", "The file embeds files", Prohibited),
        new(PdfConformanceFinding.ExternalContent, "ExternalContent", "The file refers to no external content.", "A stream refers to content outside the file", SelfContained),
    ];

    /// <summary>Checks every rule that reads the file's content, in a stable order.</summary>
    /// <param name="document">The document.</param>
    /// <param name="findings">Receives the findings.</param>
    /// <param name="cancellationToken">Stops the scan.</param>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    internal static void Check(PdfDocument document, List<PdfXFinding> findings, CancellationToken cancellationToken)
    {
        // The output intent argument is true so the scan skips its own device colour test, which the OutputIntent rule covers.
        var observations = new ConformanceScan(document).Run(null, true, cancellationToken);
        foreach (var rule in ObservationRules)
        {
            findings.Add(FromObservation(observations, rule));
        }

        var uses = ConformanceResources.Read(document, cancellationToken);
        findings.Add(CheckTransferCurves(document, uses.GraphicsStates));
        findings.Add(CheckAlternates(uses.Images));
    }

    /// <summary>Turns the scan's count of one feature into a finding.</summary>
    /// <param name="observations">The scan's observations.</param>
    /// <param name="rule">The rule.</param>
    /// <returns>The finding.</returns>
    private static PdfXFinding FromObservation(PdfConformanceObservation[] observations, ObservationRule rule)
    {
        foreach (var observation in observations)
        {
            if (observation.Kind != rule.Kind)
            {
                continue;
            }

            var detail = observation.Detail is { Length: > 0 } text ? $": {text}" : string.Empty;
            return new(rule.Name, false, $"{rule.FailMessage} ({observation.Count}){detail}.", rule.Reference);
        }

        return new(rule.Name, true, rule.PassMessage, rule.Reference);
    }

    /// <summary>Checks that no graphics state sets a transfer function other than <c>/Default</c>.</summary>
    /// <param name="document">The document.</param>
    /// <param name="states">The graphics states content uses.</param>
    /// <returns>The finding.</returns>
    private static PdfXFinding CheckTransferCurves(PdfDocument document, List<PdfDictionary> states)
    {
        var count = 0;
        foreach (var state in states)
        {
            count += state.ContainsKey(KnownName.TR) || HasCustomTransfer(document, state.Get(KnownName.TR2)) ? 1 : 0;
        }

        return count == 0
            ? new("TransferCurves", true, "No graphics state sets a transfer function.", Prohibited)
            : new("TransferCurves", false, $"{count} graphics state(s) set a transfer function, which changes how colours print.", Prohibited);
    }

    /// <summary>Determines whether a <c>/TR2</c> value is anything but absent or <c>/Default</c>.</summary>
    /// <param name="document">The document.</param>
    /// <param name="value">The value.</param>
    /// <returns><see langword="true"/> when it sets a real transfer function.</returns>
    private static bool HasCustomTransfer(PdfDocument document, PdfValue value) =>
        !value.IsNull && !(value.AsName() is { IsNone: false } name && document.Objects.Names.GetString(name) == DefaultTransfer);

    /// <summary>Checks that no image offers alternates, such as a low resolution stand-in.</summary>
    /// <param name="images">The images content draws.</param>
    /// <returns>The finding.</returns>
    private static PdfXFinding CheckAlternates(List<PdfDictionary> images)
    {
        var count = 0;
        foreach (var image in images)
        {
            count += image.ContainsKey(KnownName.Alternates) ? 1 : 0;
        }

        return count == 0
            ? new("AlternateImages", true, "No image has alternates.", Prohibited)
            : new("AlternateImages", false, $"{count} image(s) carry alternates, which PDF/X forbids.", Prohibited);
    }

    /// <summary>One scan count and the finding it becomes.</summary>
    /// <param name="Kind">The scan feature.</param>
    /// <param name="Name">The rule name.</param>
    /// <param name="PassMessage">The message when the feature is absent.</param>
    /// <param name="FailMessage">The start of the message when the feature is present.</param>
    /// <param name="Reference">The requirement's source.</param>
    private sealed record ObservationRule(PdfConformanceFinding Kind, string Name, string PassMessage, string FailMessage, string Reference);
}
