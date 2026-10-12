// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Documents;
using HyperPdf.Core.Forms.Scripting;
using HyperPdf.TestAssets;

namespace HyperPdf.Engine.Tests;

/// <summary>Tests reading form field scripts natively: each field's keystroke, format, validate and calculate actions are recognised.</summary>
public sealed class FormScriptReadingTests
{
    /// <summary>The fields of the calculated form.</summary>
    private const int Fields = 6;

    /// <summary>Every scripted field is listed with what its scripts do.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsEachFieldsScripts()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-calculated-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateCalculatedForm());
        try
        {
            using var document = new HyperPdfEngine().Open(path, null);
            var scripts = new List<FieldScripts>();
            HyperPdf.Core.Documents.DocumentFeature<IFormScriptSource>.Cast(document)!.GetScripts(0, scripts);
            var byName = scripts.ToDictionary(static s => s.Name, StringComparer.Ordinal);

            await Assert.That(scripts.Count).IsEqualTo(Fields);
            await Assert.That(byName["Price"].Keystroke.Function).IsEqualTo(FormScriptFunction.Number);
            await Assert.That(byName["Total"].Calculate.Function).IsEqualTo(FormScriptFunction.Simple);
            await Assert.That(byName["Total"].Calculate.Fields).IsEquivalentTo(["Price", "Quantity"]);
            await Assert.That(byName["Tax"].Calculate.Expression).IsEqualTo("Total * 0.1");
            await Assert.That(byName["Percent"].Validate.Function).IsEqualTo(FormScriptFunction.Range);
            await Assert.That(byName["Date"].Format.Text(0, string.Empty)).IsEqualTo("dd/mm/yyyy");
        }
        finally
        {
            File.Delete(path);
        }
    }
}
