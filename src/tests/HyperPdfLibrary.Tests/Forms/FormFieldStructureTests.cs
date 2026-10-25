// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Accessibility;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Forms;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Tests.Conformance;

namespace HyperPdfLibrary.Tests.Forms;

/// <summary>Tests that created fields keep tagged documents and PDF/A claims valid.</summary>
public sealed class FormFieldStructureTests
{
    /// <summary>The second part of ISO 19005.</summary>
    private const int PdfA2 = 2;

    /// <summary>The key a tagged sample's parent tree uses for its page; the next key is the first one free.</summary>
    private const int FirstFreeKey = 1;

    /// <summary>The name of a test field.</summary>
    private const string FieldName = "Email";

    /// <summary>The tooltip of a test field.</summary>
    private const string Tooltip = "Your email address";

    /// <summary>The rows used by the tagged test.</summary>
    private const int ThirdRow = 2;

    /// <summary>A tagged document gets a Form element for each widget, with an object reference, a struct parent and a parent tree entry.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TaggedDocumentGetsFormElementsWithObjectReferences()
    {
        using var document = FormDesignDocuments.OpenTagged();
        var created = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text(FieldName, 0));
        var radios = PdfFormFieldCreation.CreateField(
            document,
            new(PdfFieldType.RadioButton, "Pick", [FormDesignDocuments.Place(0, 1), FormDesignDocuments.Place(0, ThirdRow)]) { Value = "Choice1" });

        var widget = FormDesignDocuments.Dictionary(document, created.FieldId);
        var key = widget.GetInt32(KnownName.StructParent, -1);
        var root = document.Catalog.GetDictionary(KnownName.StructTreeRoot)!;
        var element = NameTree.FindNumber(root.GetDictionary(KnownName.ParentTree), key).AsDictionary()!;
        var reference = element.GetDictionary(KnownName.K)!;
        await Assert.That(key).IsEqualTo(FirstFreeKey);
        await Assert.That(element.IsName(KnownName.S, KnownName.Form)).IsTrue();
        await Assert.That(reference.IsName(KnownName.Type, KnownName.OBJR)).IsTrue();
        await Assert.That(reference.GetRaw(KnownName.Obj).AsReference()).IsEqualTo(created.FieldId);
        await Assert.That(element.GetRaw(KnownName.Pg).AsReference()).IsEqualTo(PdfDocumentPages.GetPage(document, 0).Id);

        var group = element.GetDictionary(KnownName.P)!;
        await Assert.That(document.Objects.Names.GetString(group.GetName(KnownName.S))).IsEqualTo("Document");
        await Assert.That(group.GetArray(KnownName.K)!.Count).IsGreaterThan(FirstFreeKey);
        await Assert.That(root.GetInt32(KnownName.ParentTreeNextKey)).IsEqualTo(FirstFreeKey + 1 + radios.Widgets.Length);
        await Assert.That(PdfDocumentPages.GetPage(document, 0).Dictionary.IsName(KnownName.Tabs, KnownName.S)).IsTrue();
    }

    /// <summary>The accessibility report finds a label and a Form element for every created widget, and the saved file passes qpdf.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task AccessibilityReportAcceptsCreatedFields()
    {
        using var document = FormDesignDocuments.OpenTagged();
        _ = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text(FieldName, 0));
        _ = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Field(PdfFieldType.CheckBox, "Agree", 1));
        _ = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Field(PdfFieldType.Signature, "Sign", ThirdRow));

        var report = await PdfDocumentAccessibility.GetAccessibilityReportAsync(document, CancellationToken.None);
        await Assert.That(report.GetCount(PdfAccessibilityCode.FormFieldNoLabel)).IsEqualTo(0);
        await Assert.That(report.GetCount(PdfAccessibilityCode.FormFieldNotTagged)).IsEqualTo(0);
        var saved = await FormDesignDocuments.SaveAsync(document);
        await Assert.That(await FormDesignDocuments.PassesQpdfAsync("tagged", saved)).IsTrue();
        await Assert.That(await FormDesignDocuments.PassesQpdfAsync("tagged-compact", FormDesignDocuments.SaveCompact(document))).IsTrue();
        using var reopened = PdfDocumentReader.Open(saved, null);
        await Assert.That((await PdfDocumentAccessibility.GetAccessibilityReportAsync(reopened, CancellationToken.None)).GetCount(PdfAccessibilityCode.FormFieldNotTagged)).IsEqualTo(0);
    }

    /// <summary>The tooltip defaults to the field name and a given tooltip replaces it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TooltipDefaultsToTheFieldName()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        var plain = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text(FieldName, 0));
        var described = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text("Other", 1) with { Tooltip = Tooltip });

        await Assert.That(FormDesignDocuments.Dictionary(document, plain.FieldId).GetText(KnownName.TU)).IsEqualTo(FieldName);
        await Assert.That(FormDesignDocuments.Dictionary(document, described.FieldId).GetText(KnownName.TU)).IsEqualTo(Tooltip);
    }

    /// <summary>An untagged document gets no struct parent and no structure tree.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task UntaggedDocumentIsNotTagged()
    {
        using var document = FormDesignDocuments.OpenPlain(1);
        var created = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text(FieldName, 0));

        await Assert.That(FormDesignDocuments.Dictionary(document, created.FieldId).ContainsKey(KnownName.StructParent)).IsFalse();
        await Assert.That(document.Catalog.ContainsKey(KnownName.StructTreeRoot)).IsFalse();
    }

    /// <summary>A PDF/A document takes buttons and signature fields, which draw no text, and the claim still validates as before.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfADocumentTakesFieldsThatDrawNoText()
    {
        using var document = OpenClaimingPdfA();
        var before = PdfAValidator.Validate(document, PdfALevel.PdfA2B).Failed.Select(static r => $"{r.Clause}:{r.Test}").ToHashSet();

        _ = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Field(PdfFieldType.CheckBox, "Agree", 0));
        _ = PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Field(PdfFieldType.Signature, "Sign", 1));

        var after = PdfAValidator.Validate(document, PdfALevel.PdfA2B).Failed.Select(static r => $"{r.Clause}:{r.Test}").ToHashSet();
        await Assert.That(after.Except(before)).IsEmpty();
        await Assert.That(document.Catalog.GetDictionary(KnownName.AcroForm)!.ContainsKey(KnownName.NeedAppearances)).IsFalse();
    }

    /// <summary>A PDF/A document refuses fields that need a font it lacks, actions, and readers drawing the field, and is left unchanged.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PdfADocumentRefusesWhatBreaksTheClaim()
    {
        using var document = OpenClaimingPdfA();

        await Assert.That(() => PdfFormFieldCreation.CreateField(document, FormDesignDocuments.Text(FieldName, 0))).Throws<InvalidOperationException>();
        var bare = FormDesignDocuments.Field(PdfFieldType.CheckBox, "A", 0) with { GenerateAppearances = false };
        var scripted = FormDesignDocuments.Field(PdfFieldType.PushButton, "B", 0) with { Actions = new() { Activate = "app.alert(1)" } };
        await Assert.That(() => PdfFormFieldCreation.CreateField(document, bare)).Throws<InvalidOperationException>();
        await Assert.That(() => PdfFormFieldCreation.CreateField(document, scripted)).Throws<InvalidOperationException>();
        await Assert.That(document.Catalog.ContainsKey(KnownName.AcroForm)).IsFalse();
    }

    /// <summary>Opens a one page document whose XMP claims PDF/A-2b.</summary>
    /// <returns>The document.</returns>
    private static PdfDocument OpenClaimingPdfA()
    {
        var pdf = ConformancePdf.Page();

        // The page already paints in device grey and RGB, so the colour rules that need an output intent fail before and after.
        pdf.Content = "0 g 0 0 1 1 re f 1 0 0 rg 0 0 1 1 re f";
        ConformancePdf.Claim(pdf, PdfA2, "B", null);
        return PdfDocumentReader.Open(pdf.ToBytes(), null);
    }
}
