// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Annotations;
using HyperPdf.Core.Archiving;
using HyperPdf.Core.Attachments;
using HyperPdf.Core.Documents;
using HyperPdf.Core.Flattening;
using HyperPdf.Core.Forms;
using HyperPdf.Core.Forms.Scripting;
using HyperPdf.Core.Ocr;
using HyperPdf.Core.Optimizing;
using HyperPdf.Core.Presentation;
using HyperPdf.Core.Printing;
using HyperPdf.Core.Protection;
using HyperPdf.Core.Reading;
using HyperPdf.Core.Redaction;
using HyperPdf.Core.Rendering;
using HyperPdf.Core.Signatures;
using HyperPdf.Core.Signatures.Signing;
namespace HyperPdf.Engine;

/// <summary>Resolves supported services without reflection, activation or stored tables.</summary>
internal static class HyperPdfFeatureRegistry
{
    /// <summary>The number of feature slots a document holds.</summary>
    private const int SlotCount = (int)FeatureSlot.FormVisibility + 1;

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
        _ when featureType == typeof(IArchiveCopyWriter) => FeatureSlot.ArchiveCopyWriter,
        _ when featureType == typeof(IPdf20CopyWriter) => FeatureSlot.Pdf20CopyWriter,
        _ when featureType == typeof(ISeparationSource) => FeatureSlot.Separations,
        _ when featureType == typeof(ITextLayoutSource) => FeatureSlot.TextLayout,
        _ when featureType == typeof(IDocumentRequirements) => FeatureSlot.Requirements,
        _ when featureType == typeof(IDocumentProtector) => FeatureSlot.Protector,
        _ when featureType == typeof(IFlattenCopyWriter) => FeatureSlot.FlattenCopyWriter,
        _ when featureType == typeof(IDocumentSigner) => FeatureSlot.Signer,
        _ when featureType == typeof(IRenderSharpness) => FeatureSlot.RenderSharpness,
        _ when featureType == typeof(IPageTransitionSource) => FeatureSlot.PageTransitions,
        _ when featureType == typeof(IDocumentPartSource) => FeatureSlot.PartSource,
        _ when featureType == typeof(IPageSemanticSource) => FeatureSlot.PageSemantics,
        _ when featureType == typeof(IDocumentXmpSource) => FeatureSlot.XmpSource,
        _ when featureType == typeof(IFormVisibility) => FeatureSlot.FormVisibility,
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
        _ when featureType == typeof(IOutlineEditor) => FeatureSlot.OutlineEditor,
        _ when featureType == typeof(IAttachmentEditor) => FeatureSlot.AttachmentEditor,
        _ when featureType == typeof(ITagEditor) => FeatureSlot.TagEditor,
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
        _ when featureType == typeof(IFormDesigner) => FeatureSlot.FormDesigner,
        _ => FeatureSlot.None,
    };

    /// <summary>Creates one lightweight service borrowing the document's resources.</summary>
    /// <param name="owner">The owning document.</param>
    /// <param name="slot">The service's slot.</param>
    /// <returns>The service.</returns>
    private static IDocumentFeature Create(HyperPdfDocument owner, FeatureSlot slot) => slot switch
    {
        FeatureSlot.Requirements => new HyperPdfRequirementsService(owner),
        FeatureSlot.Protector => new HyperPdfDocumentProtectorService(owner),
        FeatureSlot.OutlineEditor => new HyperPdfOutlineEditorService(owner),
        FeatureSlot.AttachmentEditor => new HyperPdfAttachmentEditorService(owner),
        FeatureSlot.FormDesigner => new HyperPdfFormDesignerService(owner),
        FeatureSlot.FlattenCopyWriter => new HyperPdfFlattenCopyWriterService(owner),
        FeatureSlot.Signer => new HyperPdfSignerService(owner),
        FeatureSlot.TagEditor => new HyperPdfTagEditorService(owner),
        FeatureSlot.RenderSharpness => new HyperPdfRenderSharpnessService(owner),
        FeatureSlot.PageTransitions => new HyperPdfPageTransitionService(owner),
        FeatureSlot.PartSource => new HyperPdfDocumentPartSourceService(owner),
        FeatureSlot.PageSemantics => new HyperPdfPageSemanticSourceService(owner),
        FeatureSlot.XmpSource => new HyperPdfDocumentXmpSourceService(owner),
        FeatureSlot.FormVisibility => new HyperPdfFormVisibilityService(owner),
        <=FeatureSlot.TextLayout => HyperPdfFeatureRegistry.CreateDocumentService(owner, slot),
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
        FeatureSlot.ArchiveCopyWriter => new HyperPdfArchiveCopyWriterService(owner),
        FeatureSlot.Pdf20CopyWriter => new HyperPdfPdf20CopyWriterService(owner),
        FeatureSlot.FlattenCopyWriter => new HyperPdfFlattenCopyWriterService(owner),
        FeatureSlot.Separations => new HyperPdfSeparationSourceService(owner),
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
