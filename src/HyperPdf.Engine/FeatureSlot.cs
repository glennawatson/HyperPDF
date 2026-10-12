// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

namespace HyperPdf.Engine;

/// <summary>The slot of each document service in a document's feature slots, grouped as document, editing and form services.</summary>
internal enum FeatureSlot
{
    /// <summary>Not a service the registry creates.</summary>
    None = -1,

    /// <summary>The attachment source.</summary>
    Attachments = 0,

    /// <summary>The signature source.</summary>
    Signatures = 1,

    /// <summary>The content check.</summary>
    ContentCheck = 2,

    /// <summary>The layer source.</summary>
    Layers = 3,

    /// <summary>The document optimizer.</summary>
    Optimizer = 4,

    /// <summary>The document redactor.</summary>
    Redactor = 5,

    /// <summary>The repair report.</summary>
    RepairReport = 6,

    /// <summary>The tagged structure source.</summary>
    TaggedStructure = 7,

    /// <summary>The text layout source, the last document service.</summary>
    TextLayout = 8,

    /// <summary>The page management source, the first editing service.</summary>
    PageManagement = 9,

    /// <summary>The annotation editor.</summary>
    AnnotationEditor = 10,

    /// <summary>The text box editor.</summary>
    TextBoxEditor = 11,

    /// <summary>The image signature editor.</summary>
    ImageSignatureEditor = 12,

    /// <summary>The text layer writer.</summary>
    TextLayerWriter = 13,

    /// <summary>The page exporter, the last editing service.</summary>
    PageExporter = 14,

    /// <summary>The form order, the first form service.</summary>
    FormOrder = 15,

    /// <summary>The form actions.</summary>
    FormActions = 16,

    /// <summary>The form highlight.</summary>
    FormHighlight = 17,

    /// <summary>The form filler.</summary>
    FormFiller = 18,

    /// <summary>The form script source, the last service.</summary>
    FormScripts = 19,
}
