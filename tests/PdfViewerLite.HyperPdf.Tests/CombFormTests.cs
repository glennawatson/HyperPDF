// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.TestAssets;

namespace PdfViewerLite.HyperPdf.Tests;

/// <summary>Tests reading comb fields and character limits, and filling every kind of field, natively.</summary>
public sealed class CombFormTests
{
    /// <summary>The comb's boxes and character limit.</summary>
    private const int CombBoxes = 6;

    /// <summary>The fields in the form.</summary>
    private const int FieldCount = 5;

    /// <summary>A comb field reports its boxes; ordinary fields do not; values and ticks are kept.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsAndFillsCombFields()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-comb-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateCombForm());
        try
        {
            using var document = new HyperPdfEngine().Open(path, null);
            var form = PdfViewerLite.Core.Documents.DocumentFeature<IFormFiller>.Cast(document)!;
            var fields = new List<FormField>();
            form.GetFields(0, fields);
            var name = fields.Single(static f => f.Name == "Name");
            var code = fields.Single(static f => f.Name == "Code");
            var large = fields.Last(static f => f.Kind == FormFieldKind.RadioButton);
            var setCode = form.SetText(0, code.Index, "AB1234");
            var setLarge = form.SetChecked(0, large.Index, true);
            fields.Clear();
            form.GetFields(0, fields);

            await Assert.That(fields.Count).IsEqualTo(FieldCount);
            await Assert.That(code.IsComb).IsTrue();
            await Assert.That(code.MaxLength).IsEqualTo(CombBoxes);
            await Assert.That(name.IsComb).IsFalse();
            await Assert.That(name.MaxLength).IsEqualTo(0);
            await Assert.That(setCode && setLarge).IsTrue();
            await Assert.That(fields.Single(static f => f.Name == "Code").Value).IsEqualTo("AB1234");
            await Assert.That(fields.Last(static f => f.Kind == FormFieldKind.RadioButton).IsChecked).IsTrue();
            await Assert.That(fields.First(static f => f.Kind == FormFieldKind.RadioButton).IsChecked).IsFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>A radio group's value is the on-state of the chosen button, and choosing the other button switches the first off.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RadioGroupHoldsOneChoice()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-radio-{Guid.NewGuid():N}.pdf");
        await File.WriteAllBytesAsync(path, TestPdf.CreateCombForm());
        try
        {
            using var document = new HyperPdfEngine().Open(path, null);
            var form = PdfViewerLite.Core.Documents.DocumentFeature<IFormFiller>.Cast(document)!;
            var radios = Read(form).Where(static f => f.Kind == FormFieldKind.RadioButton).ToArray();
            var offFirst = Read(form).First(static f => f.Kind == FormFieldKind.RadioButton).Value;
            _ = form.SetChecked(0, radios[0].Index, true);
            _ = form.SetChecked(0, radios[1].Index, true);
            var both = Read(form).Where(static f => f.Kind == FormFieldKind.RadioButton).ToArray();
            var offAgain = form.SetChecked(0, radios[1].Index, false);

            await Assert.That(offFirst).IsEqualTo("Off");
            await Assert.That(both[0].IsChecked).IsFalse();
            await Assert.That(both[1].IsChecked).IsTrue();
            await Assert.That(both[0].Value).IsEqualTo("Large");
            await Assert.That(offAgain).IsFalse();
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>Reads the fields of page 1.</summary>
    /// <param name="form">The filler.</param>
    /// <returns>The fields.</returns>
    private static List<FormField> Read(IFormFiller form)
    {
        var fields = new List<FormField>();
        form.GetFields(0, fields);
        return fields;
    }
}
