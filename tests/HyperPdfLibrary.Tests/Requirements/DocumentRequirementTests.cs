// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Requirements;
using HyperPdfLibrary.Tests.Document;
using static HyperPdfLibrary.Tests.Requirements.RequirementDocuments;

namespace HyperPdfLibrary.Tests.Requirements;

/// <summary>
/// Tests that the catalog's <c>/Requirements</c> array is read as data, checked against the viewer's capabilities and
/// penalties added up (ISO 32000-2 12.11), and that malformed entries and handler dictionaries are bounded.
/// </summary>
public sealed class DocumentRequirementTests
{
    /// <summary>The penalty of the unsupported optional requirement in the mixed sample.</summary>
    private const int TransitionPenalty = 20;

    /// <summary>The penalty of the unknown requirement in the mixed sample.</summary>
    private const int UnknownPenalty = 10;

    /// <summary>The penalty of the partly supported requirement in the mixed sample.</summary>
    private const int CollectionPenalty = 5;

    /// <summary>The penalty of the version requirement in the mixed sample.</summary>
    private const int VersionPenalty = 15;

    /// <summary>The number of requirements in the mixed sample.</summary>
    private const int MixedCount = 6;

    /// <summary>A penalty that, twice over, passes the critical total.</summary>
    private const int SixtyPenalty = 60;

    /// <summary>The number of malformed entries in the malformed sample.</summary>
    private const int SkippedCount = 5;

    /// <summary>The number of handlers or requirements that survive in the malformed samples.</summary>
    private const int KeptCount = 3;

    /// <summary>The number of requirement dictionaries written into the oversized array.</summary>
    private const int OversizedCount = 300;

    /// <summary>The most requirements the reader keeps.</summary>
    private const int MaxRequirements = 256;

    /// <summary>The number of handlers written into the oversized handler array.</summary>
    private const int OversizedHandlers = 40;

    /// <summary>The most handlers the reader keeps for one requirement.</summary>
    private const int MaxHandlers = 16;

    /// <summary>The length of the script name written into the oversized handler.</summary>
    private const int LongScriptLength = 5000;

    /// <summary>The most characters of a script name the reader keeps.</summary>
    private const int MaxScriptLength = 256;

    /// <summary>A document with no requirements reads as the shared empty list and assessment.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NoRequirementsIsEmpty()
    {
        using var document = StructureDocuments.Open(string.Empty);
        var list = PdfDocumentRequirements.GetRequirements(document);
        var assessment = PdfDocumentRequirements.Evaluate(document);

        await Assert.That(list).IsSameReferenceAs(PdfRequirementList.Empty);
        await Assert.That(assessment).IsSameReferenceAs(PdfRequirementAssessment.Empty);
        await Assert.That(assessment.Decision).IsEqualTo(PdfRequirementDecision.Satisfied);
        await Assert.That(assessment.AllowsProcessing).IsTrue();
    }

    /// <summary>Each requirement of a mixed PDF 2.0 file gets its own result, version and penalty.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MixedRequirementsReportEachResult()
    {
        using var document = Open(
            "[<< /Type /Requirement /S /Navigation /V /2.0 /Penalty 0 >> "
            + $"<< /S /Transitions /Penalty {TransitionPenalty} >> "
            + $"<< /S /Collection /V /1.7 /Penalty {CollectionPenalty} >> "
            + $"<< /S /Markup /V /2.1 /Penalty {VersionPenalty} >> "
            + $"<< /S /ADBE_Vendor /Penalty {UnknownPenalty} >> "
            + "<< /S /Attachment >>]");

        var assessment = PdfDocumentRequirements.Evaluate(document);
        var results = assessment.Results;

        await Assert.That(results.Count).IsEqualTo(MixedCount);
        await Assert.That(results[0].Outcome).IsEqualTo(PdfRequirementOutcome.Met);
        await Assert.That(results[0].Requirement.Version!.Text).IsEqualTo("2.0");
        await Assert.That(results[0].Requirement.Version!.IsValid).IsTrue();
        await Assert.That(results[1].Outcome).IsEqualTo(PdfRequirementOutcome.NotSupported);
        await Assert.That(results[1].AppliedPenalty).IsEqualTo(TransitionPenalty);
        await Assert.That(results[2].Outcome).IsEqualTo(PdfRequirementOutcome.PartlySupported);
        await Assert.That(results[2].Requirement.Version!.Text).IsEqualTo("1.7");
        await Assert.That(results[3].Outcome).IsEqualTo(PdfRequirementOutcome.VersionNotSupported);
        await Assert.That(results[3].Message).Contains("2.1");
        await Assert.That(results[4].Outcome).IsEqualTo(PdfRequirementOutcome.UnknownType);
        await Assert.That(results[4].HandlersDisabled).IsFalse();

        // The last one names no penalty, so it would count 100 if unmet; Attachment is supported, so it counts nothing.
        await Assert.That(results[5].Outcome).IsEqualTo(PdfRequirementOutcome.Met);
        await Assert.That(results[5].Requirement.Penalty).IsEqualTo(Critical);
        await Assert.That(results[5].Requirement.Version).IsNull();
        await Assert.That(assessment.TotalPenalty).IsEqualTo(TransitionPenalty + CollectionPenalty + VersionPenalty + UnknownPenalty);
        await Assert.That(assessment.Decision).IsEqualTo(PdfRequirementDecision.Fallback);
    }

    /// <summary>A critical unmet requirement stops processing, and the summary names the feature.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CriticalUnmetRequirementStopsProcessing()
    {
        using var document = Open("[<< /S /EnableJavaScripts >> << /S /Navigation >>]");

        var assessment = PdfDocumentRequirements.Evaluate(document);

        await Assert.That(assessment.TotalPenalty).IsEqualTo(Critical);
        await Assert.That(assessment.Decision).IsEqualTo(PdfRequirementDecision.Critical);
        await Assert.That(assessment.AllowsProcessing).IsFalse();
        await Assert.That(assessment.ExceedsDisplayLimit).IsFalse();
        await Assert.That(assessment.Summary).Contains("Running scripts");
    }

    /// <summary>Penalties from several requirements add up, and a total over 100 passes the display limit.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PenaltiesAddUpAcrossRequirements()
    {
        using var document = Open($"[<< /S /Transitions /Penalty {SixtyPenalty} >> << /S /Multimedia /Penalty {SixtyPenalty} >>]");

        var assessment = PdfDocumentRequirements.Evaluate(document);

        await Assert.That(assessment.TotalPenalty).IsEqualTo(SixtyPenalty + SixtyPenalty);
        await Assert.That(assessment.Decision).IsEqualTo(PdfRequirementDecision.Critical);
        await Assert.That(assessment.ExceedsDisplayLimit).IsTrue();
    }

    /// <summary>An unmet requirement with a small total allows processing and the normal fallback.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OptionalUnmetRequirementAllowsFallback()
    {
        using var document = Open("[<< /S /Transitions /Penalty 0 >>]");

        var assessment = PdfDocumentRequirements.Evaluate(document);

        await Assert.That(assessment.TotalPenalty).IsEqualTo(0);
        await Assert.That(assessment.Decision).IsEqualTo(PdfRequirementDecision.Fallback);
        await Assert.That(assessment.AllowsProcessing).IsTrue();
        await Assert.That(assessment.Summary).Contains("Page transition effects");
    }

    /// <summary>A narrower capability table changes the result without changing the reader.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CustomCapabilitiesAreUsed()
    {
        using var document = Open("[<< /S /Navigation >>]");
        var none = new PdfRequirementCapabilities([new("Navigation", "Links", PdfRequirementSupport.Unsupported, string.Empty, "Links are off.")]);

        var assessment = PdfDocumentRequirements.Evaluate(document, none);

        await Assert.That(assessment.Decision).IsEqualTo(PdfRequirementDecision.Critical);
        await Assert.That(assessment.Results[0].Feature).IsEqualTo("Links");
    }

    /// <summary>The asynchronous forms give the same results and honour cancellation.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AsyncFormsMatchAndCancel()
    {
        using var document = Open("[<< /S /Transitions /Penalty 0 >>]");
        using var cancelled = new CancellationTokenSource();
        await cancelled.CancelAsync();

        var assessment = await PdfDocumentRequirements.EvaluateAsync(document, PdfRequirementCapabilities.HyperPdf, CancellationToken.None);
        var list = await PdfDocumentRequirements.GetRequirementsAsync(document, CancellationToken.None);

        await Assert.That(assessment.Decision).IsEqualTo(PdfRequirementDecision.Fallback);
        await Assert.That(list.Requirements.Count).IsEqualTo(1);
        _ = await Assert.ThrowsAsync<OperationCanceledException>(async () => _ = await PdfDocumentRequirements.GetRequirementsAsync(document, cancelled.Token));
    }

    /// <summary>Entries that are not requirement dictionaries are skipped and counted; bad values take their defaults.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MalformedEntriesAreSkippedOrDefaulted()
    {
        using var document = Open(
            "[42 << /Penalty 5 >> << /S 7 >> (text) null << /S /Navigation /Penalty /High /V (2.0) /RH 9 >> "
            + "<< /S /Navigation /Penalty 500 >> << /S /Navigation /Penalty -5 /V /2 >>]");

        var list = PdfDocumentRequirements.GetRequirements(document);

        await Assert.That(list.SkippedEntries).IsEqualTo(SkippedCount);
        await Assert.That(list.Requirements.Count).IsEqualTo(KeptCount);
        await Assert.That(list.Requirements[0].Penalty).IsEqualTo(Critical);
        await Assert.That(list.Requirements[0].Version!.IsValid).IsFalse();
        await Assert.That(list.Requirements[0].Handlers.Count).IsEqualTo(0);
        await Assert.That(list.Requirements[1].Penalty).IsEqualTo(Critical);
        await Assert.That(list.Requirements[2].Penalty).IsEqualTo(0);
        await Assert.That(list.Requirements[2].Version!.IsValid).IsFalse();
    }

    /// <summary>A /Requirements value that is not an array is one skipped entry, not an error.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NonArrayRequirementsIsOneSkippedEntry()
    {
        using var document = Open("<< /S /Navigation >>");

        var list = PdfDocumentRequirements.GetRequirements(document);
        var assessment = PdfDocumentRequirements.Evaluate(document);

        await Assert.That(list.Requirements.Count).IsEqualTo(0);
        await Assert.That(list.SkippedEntries).IsEqualTo(1);
        await Assert.That(assessment.Decision).IsEqualTo(PdfRequirementDecision.Satisfied);
    }

    /// <summary>An array longer than the limit is cut at the limit and says so.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OversizedArrayIsTruncated()
    {
        using var document = Open(Repeated(OversizedCount));

        var list = PdfDocumentRequirements.GetRequirements(document);

        await Assert.That(list.Requirements.Count).IsEqualTo(MaxRequirements);
        await Assert.That(list.WasTruncated).IsTrue();
    }

    /// <summary>Version names must be two or more whole numbers; an extension dictionary is a vendor level the viewer does not meet.</summary>
    /// <param name="version">The /V value.</param>
    /// <param name="valid">Whether the reader accepts it.</param>
    /// <param name="extension">Whether it is an extension dictionary.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("/2.0", true, false)]
    [Arguments("/1.7.1", true, false)]
    [Arguments("/2", false, false)]
    [Arguments("/2..0", false, false)]
    [Arguments("/2.x", false, false)]
    [Arguments("<< /ADBE 3 >>", true, true)]
    [Arguments("<< /ADBE 3 /ISO_ 1 >>", false, true)]
    public async Task VersionsAreValidated(string version, bool valid, bool extension)
    {
        using var document = Open($"[<< /S /Navigation /V {version} >>]");

        var read = PdfDocumentRequirements.GetRequirements(document).Requirements[0].Version!;

        await Assert.That(read.IsValid).IsEqualTo(valid);
        await Assert.That(read.IsExtension).IsEqualTo(extension);
    }

    /// <summary>A vendor extension version is not one the viewer meets; an unreadable version is read as absent.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ExtensionVersionIsUnmetAndUnreadableVersionIsIgnored()
    {
        using var document = Open("[<< /S /Navigation /V << /ADBE 3 >> /Penalty 7 >> << /S /Navigation /V /oops >>]");

        var results = PdfDocumentRequirements.Evaluate(document).Results;

        await Assert.That(results[0].Outcome).IsEqualTo(PdfRequirementOutcome.VersionNotSupported);
        await Assert.That(results[1].Outcome).IsEqualTo(PdfRequirementOutcome.Met);
    }

    /// <summary>Handlers are read as data: the script name of a JS handler is kept, an unknown handler is flagged, and nothing runs.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task HandlersAreReadAsDataOnly()
    {
        using var document = StructureDocuments.Open(
            "/Version /2.0 /Names << /JavaScript << /Names [(CheckTransitions) 4 0 R] >> >> /Requirements "
            + "[<< /S /Transitions /Penalty 0 /RH [<< /Type /ReqHandler /S /JS /Script (CheckTransitions) >> << /S /NoOp /Script (Ignored) >> << /S /Other >> 5 << /Type /ReqHandler >>] >> "
            + "<< /S /Navigation /RH << /S /JS /Script (Single) >> >>]",
            "<< /S /JavaScript /JS (app.alert('this must never run')) >>");

        var list = PdfDocumentRequirements.GetRequirements(document);
        var results = PdfDocumentRequirements.Evaluate(document).Results;

        // Reading and checking leave the document untouched; the script is named but never loaded or run.
        await Assert.That(StoreEditing.HasChanges(document.Objects)).IsFalse();
        var handlers = list.Requirements[0].Handlers;
        await Assert.That(handlers.Count).IsEqualTo(KeptCount);
        await Assert.That(handlers[0].Type).IsEqualTo("JS");
        await Assert.That(handlers[0].ScriptName).IsEqualTo("CheckTransitions");
        await Assert.That(handlers[0].IsRecognized).IsTrue();
        await Assert.That(handlers[1].Type).IsEqualTo("NoOp");
        await Assert.That(handlers[1].ScriptName).IsNull();
        await Assert.That(handlers[2].IsRecognized).IsFalse();
        await Assert.That(list.Requirements[1].Handlers[0].ScriptName).IsEqualTo("Single");
        await Assert.That(results[0].HandlersDisabled).IsTrue();
    }

    /// <summary>A handler array longer than the limit and a very long script name are cut.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task OversizedHandlersAreBounded()
    {
        var handlers = new StringBuilder("[");
        for (var i = 0; i < OversizedHandlers; i++)
        {
            _ = handlers.Append("<< /S /NoOp >> ");
        }

        _ = handlers.Append(']');
        using var many = Open($"[<< /S /Navigation /RH {handlers} >>]");
        using var longName = Open($"[<< /S /Navigation /RH << /S /JS /Script ({new string('a', LongScriptLength)}) >> >>]");

        var manyHandlers = PdfDocumentRequirements.GetRequirements(many).Requirements[0].Handlers;
        var script = PdfDocumentRequirements.GetRequirements(longName).Requirements[0].Handlers[0].ScriptName;

        await Assert.That(manyHandlers.Count).IsEqualTo(MaxHandlers);
        await Assert.That(script!.Length).IsEqualTo(MaxScriptLength);
    }

    /// <summary>An Encryption requirement is met only for a handler and algorithm the viewer reads.</summary>
    /// <param name="encrypt">The /Encrypt dictionary text.</param>
    /// <param name="met">Whether the requirement is met.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("<< /Filter /Standard /V 5 /R 6 >>", true)]
    [Arguments("<< /Filter /Adobe.PubSec /V 4 >>", true)]
    [Arguments("<< /Filter /Vendor /V 4 >>", false)]
    [Arguments("<< /Filter /Standard /V 9 /R 12 >>", false)]
    [Arguments("null", false)]
    public async Task EncryptionRequirementChecksTheSettings(string encrypt, bool met)
    {
        using var document = Open($"[<< /S /Encryption /Encrypt {encrypt} >>]");

        var result = PdfDocumentRequirements.Evaluate(document).Results[0];

        await Assert.That(result.IsMet).IsEqualTo(met);
        await Assert.That(result.Requirement.Encryption).IsNotNull();
    }
}
