// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>Tests that the engine reports a document's requirements in the app's terms.</summary>
public sealed class DocumentRequirementsTests
{
    /// <summary>A file with an unmet critical requirement reports it, and a file with no requirements reports none.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task EngineReportsRequirementLevels()
    {
        var critical = Path.Combine(Path.GetTempPath(), $"hyperpdf-requirements-{Guid.NewGuid():N}.pdf");
        var optional = Path.Combine(Path.GetTempPath(), $"hyperpdf-requirements-{Guid.NewGuid():N}.pdf");
        var plain = TestPdf.WriteTempFile(1);
        await File.WriteAllBytesAsync(critical, FormRuntimeSamples.Create(string.Empty, "/Requirements [<< /S /Multimedia >>]"));
        await File.WriteAllBytesAsync(optional, FormRuntimeSamples.Create(string.Empty, "/Requirements [<< /S /Multimedia /Penalty 0 >> << /S /Markup >>]"));
        try
        {
            using var criticalDocument = new HyperPdfEngine().Open(critical, null);
            using var optionalDocument = new HyperPdfEngine().Open(optional, null);
            using var plainDocument = new HyperPdfEngine().Open(plain, null);

            var stopped = DocumentFeature<IDocumentRequirements>.Cast(criticalDocument)!.Check();
            var fallback = DocumentFeature<IDocumentRequirements>.Cast(optionalDocument)!.Check();
            var none = DocumentFeature<IDocumentRequirements>.Cast(plainDocument)!.Check();

            await Assert.That(stopped.Level).IsEqualTo(RequirementLevel.Critical);
            await Assert.That(stopped.StopsProcessing).IsTrue();
            await Assert.That(stopped.Details.Count).IsEqualTo(1);
            await Assert.That(fallback.Level).IsEqualTo(RequirementLevel.Fallback);
            await Assert.That(fallback.StopsProcessing).IsFalse();
            await Assert.That(fallback.Details.Count).IsEqualTo(1);
            await Assert.That(none).IsSameReferenceAs(RequirementReport.None);
        }
        finally
        {
            File.Delete(critical);
            File.Delete(optional);
            File.Delete(plain);
        }
    }
}
