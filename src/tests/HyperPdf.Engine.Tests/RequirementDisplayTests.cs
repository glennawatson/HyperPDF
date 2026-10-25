// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using HyperPdf.Core.Documents;
using HyperPdfLibrary.Requirements;

namespace HyperPdf.Engine.Tests;

/// <summary>Checks typed requirement messages preserve values in every shipped language.</summary>
[Category("Localisation")]
[NotInParallel]
public sealed class RequirementDisplayTests
{
    /// <summary>The penalty used by the document fixture.</summary>
    private const int Penalty = 37;

    /// <summary>The number of unmet entries needed to exercise the remainder label.</summary>
    private const int ManyFeatures = 6;

    /// <summary>The feature type used by the fixture.</summary>
    private const string TransitionType = "Transitions";

    /// <summary>The English feature title used as a generated resource key.</summary>
    private const string TransitionTitle = "Page transition effects";

    /// <summary>The French feature title expected after a live culture change.</summary>
    private const string FrenchTransitionTitle = "Effets de transition entre les pages";

    /// <summary>A document identifier that also happens to be an interface resource key.</summary>
    private const string UnknownRequirementType = "Save";

    /// <summary>A saved removal notice follows language changes while unknown document identifiers remain data.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RemovedRequirement_FollowsLiveCultureAndPreservesUnknownType()
    {
        var original = CultureInfo.CurrentUICulture;
        var originalDefault = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            var known = new RemovedRequirement(TransitionType, TransitionTitle);
            var unknown = new RemovedRequirement(UnknownRequirementType, null);
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("fr"));
            await Assert.That(known.Text).Contains(FrenchTransitionTitle);
            await Assert.That(unknown.Text).Contains(UnknownRequirementType);
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("ar"));
            await Assert.That(known.Text).DoesNotContain(TransitionTitle);
            await Assert.That(unknown.Text).Contains(UnknownRequirementType);
        }
        finally
        {
            InterfaceCulture.Apply(UiText, original);
            CultureInfo.DefaultThreadCurrentUICulture = originalDefault;
        }
    }

    /// <summary>A retained typed report refreshes its summary and details without reopening the document.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RetainedReport_FollowsLiveCultureChanges()
    {
        var original = CultureInfo.CurrentUICulture;
        var originalDefault = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("en"));
            var requirement = new PdfRequirement(TransitionType, null, Penalty, [], null);
            var result = new PdfRequirementResult(requirement, PdfRequirementOutcome.NotSupported, TransitionTitle, string.Empty, true);
            var report = RequirementMapping.Describe(new([result], Penalty, PdfRequirementDecision.Critical, string.Empty));
            await Assert.That(RequirementNotices.Describe(report)).Contains(TransitionTitle);
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("fr"));
            await Assert.That(RequirementNotices.Describe(report)).Contains(FrenchTransitionTitle);
            await Assert.That(RequirementNotices.DescribeDetails(report)).Contains("ne prend pas en charge");
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("ar"));
            await Assert.That(RequirementNotices.Describe(report)).DoesNotContain(TransitionTitle);
            await Assert.That(RequirementNotices.DescribeDetails(report)).Contains(Penalty.ToString(CultureInfo.CurrentCulture));
            await Assert.That(report.StopsProcessing).IsTrue();
        }
        finally
        {
            InterfaceCulture.Apply(UiText, original);
            CultureInfo.DefaultThreadCurrentUICulture = originalDefault;
        }
    }

    /// <summary>Typed version and penalty fields survive translation.</summary>
    /// <param name="culture">The interface culture.</param>
    /// <param name="title">The translated feature title.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("en", "Page transition effects")]
    [Arguments("fr", FrenchTransitionTitle)]
    [Arguments("de", "Seitenübergangseffekte")]
    [Arguments("zh-Hans", "页面过渡效果")]
    [Arguments("ar", "تأثيرات انتقال الصفحات")]
    [Arguments("es", "Efectos de transición de página")]
    [Arguments("pt", "Efeitos de transição de página")]
    public async Task Describe_LocalizesTypedVersionAndPenalty(string culture, string title)
    {
        var original = CultureInfo.CurrentUICulture;
        var originalDefault = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo(culture));
            var requirement = new PdfRequirement(TransitionType, new("9.7", false, true), Penalty, [], null);
            var result = new PdfRequirementResult(requirement, PdfRequirementOutcome.VersionNotSupported, TransitionTitle, "unused English text", true);
            var text = RequirementDisplay.Describe(result);
            await Assert.That(text).Contains(title);
            await Assert.That(text).Contains("9.7");
            await Assert.That(text).Contains(Penalty.ToString(CultureInfo.CurrentCulture));
            await Assert.That(text).DoesNotContain("unused English text");
        }
        finally
        {
            InterfaceCulture.Apply(UiText, original);
            CultureInfo.DefaultThreadCurrentUICulture = originalDefault;
        }
    }

    /// <summary>Unknown PDF requirement names remain document data inside a translated sentence.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Describe_UnknownTypePreservesDocumentName()
    {
        var original = CultureInfo.CurrentUICulture;
        var originalDefault = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("de"));
            var requirement = new PdfRequirement("VendorFeature42", null, Penalty, [], null);
            var result = new PdfRequirementResult(requirement, PdfRequirementOutcome.UnknownType, requirement.Type, string.Empty, false);
            await Assert.That(RequirementDisplay.Describe(result)).Contains("eine Anforderung namens VendorFeature42");
            await Assert.That(RequirementDisplay.Summary(PdfRequirementAssessment.Empty)).IsEqualTo(string.Empty);
        }
        finally
        {
            InterfaceCulture.Apply(UiText, original);
            CultureInfo.DefaultThreadCurrentUICulture = originalDefault;
        }
    }

    /// <summary>Every known outcome uses the translated feature and typed result rather than the library message.</summary>
    /// <param name="outcome">The checked outcome.</param>
    /// <param name="expected">The translated sentence fragment.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfRequirementOutcome.Met, "pris en charge.")]
    [Arguments(PdfRequirementOutcome.PartlySupported, "ne prend en charge qu’une partie")]
    [Arguments(PdfRequirementOutcome.NotSupported, "ne prend pas en charge")]
    public async Task Describe_FormatsKnownOutcomes(PdfRequirementOutcome outcome, string expected)
    {
        var original = CultureInfo.CurrentUICulture;
        var originalDefault = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("fr"));
            var requirement = new PdfRequirement(TransitionType, null, Penalty, [], null);
            var result = new PdfRequirementResult(requirement, outcome, TransitionTitle, string.Empty, true);
            await Assert.That(RequirementDisplay.Describe(result)).Contains(expected);
            var decision = outcome == PdfRequirementOutcome.Met ? PdfRequirementDecision.Satisfied : PdfRequirementDecision.Critical;
            var assessment = new PdfRequirementAssessment([result], Penalty, decision, "unused English summary");
            var summary = RequirementDisplay.Summary(assessment);
            await Assert.That(summary).DoesNotContain("unused English summary");
            if (!result.IsMet)
            {
                await Assert.That(summary).Contains(FrenchTransitionTitle);
            }
        }
        finally
        {
            InterfaceCulture.Apply(UiText, original);
            CultureInfo.DefaultThreadCurrentUICulture = originalDefault;
        }
    }

    /// <summary>Long summaries skip met entries and translate their remaining feature count.</summary>
    /// <param name="decision">The processing decision.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments(PdfRequirementDecision.Critical)]
    [Arguments(PdfRequirementDecision.Fallback)]
    public async Task Summary_CountsOnlyUnmetFeatures(PdfRequirementDecision decision)
    {
        var original = CultureInfo.CurrentUICulture;
        var originalDefault = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("fr"));
            var requirement = new PdfRequirement(TransitionType, null, Penalty, [], null);
            var results = new List<PdfRequirementResult> { new(requirement, PdfRequirementOutcome.Met, "Never display this met entry", string.Empty, true) };
            for (var index = 0; index < ManyFeatures; index++)
            {
                results.Add(new(requirement, PdfRequirementOutcome.NotSupported, TransitionTitle, string.Empty, true));
            }

            var text = RequirementDisplay.Summary(new(results, Penalty, decision, string.Empty));
            await Assert.That(text).Contains("et 1 autres");
            await Assert.That(text).DoesNotContain("Never display this met entry");
            await Assert.That(text).Contains(FrenchTransitionTitle);
        }
        finally
        {
            InterfaceCulture.Apply(UiText, original);
            CultureInfo.DefaultThreadCurrentUICulture = originalDefault;
        }
    }

    /// <summary>Missing optional versions and unknown capability rows are rendered without parsing library prose.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Describe_HandlesMissingVersionAndCapability()
    {
        var original = CultureInfo.CurrentUICulture;
        var originalDefault = CultureInfo.DefaultThreadCurrentUICulture;
        try
        {
            InterfaceCulture.Apply(UiText, CultureInfo.GetCultureInfo("en"));
            var requirement = new PdfRequirement("VendorFeature42", null, Penalty, [], null);
            var result = new PdfRequirementResult(requirement, PdfRequirementOutcome.VersionNotSupported, "Vendor feature title", string.Empty, false);
            await Assert.That(RequirementDisplay.Describe(result)).IsEqualTo(
                "Vendor feature title: the file asks for version , which HyperPDF does not provide. Penalty 37 of 100.");
        }
        finally
        {
            InterfaceCulture.Apply(UiText, original);
            CultureInfo.DefaultThreadCurrentUICulture = originalDefault;
        }
    }
}
