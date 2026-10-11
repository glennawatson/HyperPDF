// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using PdfViewerLite.Core.Annotations;
using PdfViewerLite.Core.Attachments;
using PdfViewerLite.Core.Documents;
using PdfViewerLite.Core.Forms;
using PdfViewerLite.Core.Forms.Scripting;
using PdfViewerLite.Core.Ocr;
using PdfViewerLite.Core.Optimizing;
using PdfViewerLite.Core.Printing;
using PdfViewerLite.Core.Reading;
using PdfViewerLite.Core.Redaction;
using PdfViewerLite.Core.Signatures;
namespace PdfViewerLite.HyperPdf;

/// <summary>Resolves supported services without reflection, activation or stored tables.</summary>
internal static class HyperPdfFeatureRegistry
{
    /// <summary>The number of feature slots a document holds.</summary>
    private const int SlotCount = (int)FeatureSlot.FormScripts + 1;

    /// <summary>Gets a stable service while leaving feature resources lazy.</summary>
    /// <param name="owner">The owning document.</param>
    /// <param name="featureType">The feature interface type.</param>
    /// <returns>The feature, or null when unsupported.</returns>
    internal static IDocumentFeature? Get(HyperPdfDocument owner, Type featureType)
    {
        lock (owner.EditGate)
        {
            ObjectDisposedException.ThrowIf(owner.IsDisposed, owner);
            return GetOpen(owner, featureType);
        }
    }

    /// <summary>Resolves a feature with the owner gate held.</summary>
    /// <param name="owner">The owning document.</param>
    /// <param name="featureType">The feature interface type.</param>
    /// <returns>The feature, or null when unsupported.</returns>
    private static IDocumentFeature? GetOpen(HyperPdfDocument owner, Type featureType)
    {
        var slot = HyperPdfFeatureRegistry.SlotOf(featureType);
        if (slot == FeatureSlot.None)
        {
            return null;
        }

        var slots = owner.FeatureSlots ??= new IDocumentFeature?[SlotCount];
        var feature = slots[(int)slot] ??= HyperPdfFeatureRegistry.Create(owner, slot);
        return feature;
    }

    /// <summary>Finds the slot of a feature interface.</summary>
    /// <param name="featureType">The feature interface type.</param>
    /// <returns>The slot, or <see cref="FeatureSlot.None"/>.</returns>
    private static FeatureSlot SlotOf(Type featureType)
    {
        var slot = HyperPdfFeatureRegistry.DocumentSlotOf(featureType);
        if (slot == FeatureSlot.None)
        {
            slot = HyperPdfFeatureRegistry.EditingSlotOf(featureType);
        }

        return slot == FeatureSlot.None ? HyperPdfFeatureRegistry.FormSlotOf(featureType) : slot;
    }

    /// <summary>Finds the slot of a document service interface.</summary>
    /// <param name="featureType">The feature interface type.</param>
    /// <returns>The slot, or <see cref="FeatureSlot.None"/>.</returns>
    private static FeatureSlot DocumentSlotOf(Type featureType) => featureType switch
    {
        _ when featureType == typeof(IAttachmentSource) => FeatureSlot.Attachments,
        _ when featureType == typeof(ISignatureSource) => FeatureSlot.Signatures,
        _ when featureType == typeof(IContentCheck) => FeatureSlot.ContentCheck,
        _ when featureType == typeof(ILayerSource) => FeatureSlot.Layers,
        _ when featureType == typeof(IDocumentOptimizer) => FeatureSlot.Optimizer,
        _ when featureType == typeof(IDocumentRedactor) => FeatureSlot.Redactor,
        _ when featureType == typeof(IRepairReport) => FeatureSlot.RepairReport,
        _ when featureType == typeof(ITaggedStructureSource) => FeatureSlot.TaggedStructure,
        _ when featureType == typeof(ITextLayoutSource) => FeatureSlot.TextLayout,
        _ => FeatureSlot.None,
    };

    /// <summary>Finds the slot of an editing service interface.</summary>
    /// <param name="featureType">The feature interface type.</param>
    /// <returns>The slot, or <see cref="FeatureSlot.None"/>.</returns>
    private static FeatureSlot EditingSlotOf(Type featureType) => featureType switch
    {
        _ when featureType == typeof(IPageManagementSource) => FeatureSlot.PageManagement,
        _ when featureType == typeof(IAnnotationEditor) => FeatureSlot.AnnotationEditor,
        _ when featureType == typeof(ITextBoxEditor) => FeatureSlot.TextBoxEditor,
        _ when featureType == typeof(IImageSignatureEditor) => FeatureSlot.ImageSignatureEditor,
        _ when featureType == typeof(ITextLayerWriter) => FeatureSlot.TextLayerWriter,
        _ when featureType == typeof(IPageExporter) => FeatureSlot.PageExporter,
        _ => FeatureSlot.None,
    };

    /// <summary>Finds the slot of a form service interface.</summary>
    /// <param name="featureType">The feature interface type.</param>
    /// <returns>The slot, or <see cref="FeatureSlot.None"/>.</returns>
    private static FeatureSlot FormSlotOf(Type featureType) => featureType switch
    {
        _ when featureType == typeof(IFormOrder) => FeatureSlot.FormOrder,
        _ when featureType == typeof(IFormActions) => FeatureSlot.FormActions,
        _ when featureType == typeof(IFormHighlight) => FeatureSlot.FormHighlight,
        _ when featureType == typeof(IFormFiller) => FeatureSlot.FormFiller,
        _ when featureType == typeof(IFormScriptSource) => FeatureSlot.FormScripts,
        _ => FeatureSlot.None,
    };

    /// <summary>Creates one lightweight service borrowing the document's resources.</summary>
    /// <param name="owner">The owning document.</param>
    /// <param name="slot">The service's slot.</param>
    /// <returns>The service.</returns>
    private static IDocumentFeature Create(HyperPdfDocument owner, FeatureSlot slot) => slot switch
    {
        <= FeatureSlot.TextLayout => HyperPdfFeatureRegistry.CreateDocumentService(owner, slot),
        <= FeatureSlot.PageExporter => HyperPdfFeatureRegistry.CreateEditingService(owner, slot),
        _ => HyperPdfFeatureRegistry.CreateFormService(owner, slot),
    };

    /// <summary>Creates a document service.</summary>
    /// <param name="owner">The owning document.</param>
    /// <param name="slot">The service's slot.</param>
    /// <returns>The service.</returns>
    private static IDocumentFeature CreateDocumentService(HyperPdfDocument owner, FeatureSlot slot) => slot switch
    {
        FeatureSlot.Attachments => new HyperPdfAttachmentSourceService(owner),
        FeatureSlot.Signatures => new HyperPdfSignatureSourceService(owner),
        FeatureSlot.ContentCheck => new HyperPdfContentCheckService(owner),
        FeatureSlot.Layers => new HyperPdfLayerSourceService(owner),
        FeatureSlot.Optimizer => new HyperPdfDocumentOptimizerService(owner),
        FeatureSlot.Redactor => new HyperPdfDocumentRedactorService(owner),
        FeatureSlot.RepairReport => new HyperPdfRepairReportService(owner),
        FeatureSlot.TaggedStructure => new HyperPdfTaggedStructureSourceService(owner),
        _ => new HyperPdfTextLayoutSourceService(owner),
    };

    /// <summary>Creates an editing service.</summary>
    /// <param name="owner">The owning document.</param>
    /// <param name="slot">The service's slot.</param>
    /// <returns>The service.</returns>
    private static IDocumentFeature CreateEditingService(HyperPdfDocument owner, FeatureSlot slot) => slot switch
    {
        FeatureSlot.PageManagement => new HyperPdfPageManagementSourceService(owner),
        FeatureSlot.AnnotationEditor => new HyperPdfAnnotationEditorService(owner),
        FeatureSlot.TextBoxEditor => new HyperPdfTextBoxEditorService(owner),
        FeatureSlot.ImageSignatureEditor => new HyperPdfImageSignatureEditorService(owner),
        FeatureSlot.TextLayerWriter => new HyperPdfTextLayerWriterService(owner),
        _ => new HyperPdfPageExporterService(owner),
    };

    /// <summary>Creates a form service.</summary>
    /// <param name="owner">The owning document.</param>
    /// <param name="slot">The service's slot.</param>
    /// <returns>The service.</returns>
    private static IDocumentFeature CreateFormService(HyperPdfDocument owner, FeatureSlot slot) => slot switch
    {
        FeatureSlot.FormOrder => new HyperPdfFormOrderService(owner),
        FeatureSlot.FormActions => new HyperPdfFormActionsService(owner),
        FeatureSlot.FormHighlight => new HyperPdfFormHighlightService(owner),
        FeatureSlot.FormFiller => new HyperPdfFormFillerService(owner),
        _ => new HyperPdfFormScriptSourceService(owner),
    };
}
