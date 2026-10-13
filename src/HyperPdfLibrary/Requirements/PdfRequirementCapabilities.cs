// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Requirements;

/// <summary>
/// The table of requirement types a viewer knows and how much of each it provides (ISO 32000-2 §12.11.2, Table 275).
/// <see cref="HyperPdf"/> describes this code base. An application that turns a feature off can build a narrower table.
/// </summary>
[DebuggerDisplay("PdfRequirementCapabilities: {Entries.Count} types")]
public sealed class PdfRequirementCapabilities
{
    /// <summary>The version of PDF the viewer reads, and so the version it can meet for any requirement defined by ISO 32000-2.</summary>
    private const string PdfVersion = "2.0";

    /// <summary>The version text of a feature the viewer does not provide.</summary>
    private const string NoVersion = "";

    /// <summary>The note of a feature the viewer provides fully.</summary>
    private const string NoNote = "";

    /// <summary>The viewer's capabilities, one row for each requirement type.</summary>
    private static readonly PdfViewerCapability[] HyperPdfRows =
    [
        new("OCInteract", "Showing and switching layers", PdfRequirementSupport.Supported, PdfVersion, NoNote),
        new("OCAutoStates", "Automatic layer states for viewing, printing and zooming", PdfRequirementSupport.Unsupported, NoVersion, "Layers keep the on or off state the file saved."),
        new("AcroFormInteract", "Filling in forms", PdfRequirementSupport.Partial, PdfVersion, "Forms can be filled and simple calculations run, but form scripts never run."),
        new("Navigation", "Links and the outline", PdfRequirementSupport.Supported, PdfVersion, NoNote),
        new("Markup", "Adding and changing comments and markings", PdfRequirementSupport.Supported, PdfVersion, NoNote),
        new("3DMarkup", "Notes and markings on 3D models", PdfRequirementSupport.Unsupported, NoVersion, "3D models are not shown."),
        new("Multimedia", "Playing sound and video", PdfRequirementSupport.Unsupported, NoVersion, "Sound and video are not played."),
        new("U3D", "3D models in U3D format", PdfRequirementSupport.Unsupported, NoVersion, "3D models are not shown."),
        new("PRC", "3D models in PRC format", PdfRequirementSupport.Unsupported, NoVersion, "3D models are not shown."),
        new("Action", "Actions", PdfRequirementSupport.Partial, PdfVersion, "Form, layer, page and link actions run. Sound, video, 3D, script and transition actions do not."),
        new("EnableJavaScripts", "Running scripts", PdfRequirementSupport.Unsupported, NoVersion, "HyperPDF never runs scripts in a document."),
        new("Attachment", "Listing and saving attached files", PdfRequirementSupport.Supported, PdfVersion, NoNote),
        new("AttachmentEditing", "Adding and removing attached files", PdfRequirementSupport.Unsupported, NoVersion, "Attached files can be listed and saved, not changed."),
        new("Collection", "Showing a portfolio of files", PdfRequirementSupport.Partial, PdfVersion, "The files are listed and can be saved, but the portfolio layout and its columns are not shown."),
        new("CollectionEditing", "Changing a portfolio of files", PdfRequirementSupport.Unsupported, NoVersion, "A portfolio cannot be changed."),
        new("DigSigValidation", "Checking digital signatures", PdfRequirementSupport.Supported, PdfVersion, NoNote),
        new("DigSig", "Signing with a certificate", PdfRequirementSupport.Supported, PdfVersion, NoNote),
        new("DigSigMDP", "Checking changes to a signed file", PdfRequirementSupport.Partial, PdfVersion, "Signatures are checked, but the rules for allowed changes are not reported."),
        new("RichMedia", "Playing rich media", PdfRequirementSupport.Unsupported, NoVersion, "Rich media is not played."),
        new("Geospatial2D", "Map positions on pages", PdfRequirementSupport.Partial, PdfVersion, "The map data is read, but positions are not shown to the reader."),
        new("Geospatial3D", "Map positions in 3D models", PdfRequirementSupport.Unsupported, NoVersion, "3D models are not shown."),
        new("DPartInteract", "Browsing the parts of a document", PdfRequirementSupport.Unsupported, NoVersion, "Document parts are not shown."),
        new("SeparationSimulation", "Previewing colour separations", PdfRequirementSupport.Supported, PdfVersion, NoNote),
        new("Transitions", "Page transition effects", PdfRequirementSupport.Unsupported, NoVersion, "Pages change without the effect."),
        new("Encryption", "The file's encryption settings", PdfRequirementSupport.Supported, PdfVersion, NoNote),
    ];

    /// <summary>Initializes a new instance of the <see cref="PdfRequirementCapabilities"/> class.</summary>
    /// <param name="entries">The rows, one for each requirement type the viewer knows.</param>
    /// <exception cref="ArgumentNullException"><paramref name="entries"/> is <see langword="null"/>.</exception>
    public PdfRequirementCapabilities(IReadOnlyList<PdfViewerCapability> entries)
    {
        ArgumentNullException.ThrowIfNull(entries);
        Entries = entries;
    }

    /// <summary>Gets what HyperPDF provides.</summary>
    public static PdfRequirementCapabilities HyperPdf { get; } = new(HyperPdfRows);

    /// <summary>Gets the rows of the table.</summary>
    public IReadOnlyList<PdfViewerCapability> Entries { get; }

    /// <summary>Finds the row of a requirement type.</summary>
    /// <param name="type">The requirement type name, compared exactly.</param>
    /// <returns>The row, or <see langword="null"/> when the viewer does not know the type.</returns>
    public PdfViewerCapability? Find(string type)
    {
        for (var i = 0; i < Entries.Count; i++)
        {
            if (string.Equals(Entries[i].Type, type, StringComparison.Ordinal))
            {
                return Entries[i];
            }
        }

        return null;
    }
}
