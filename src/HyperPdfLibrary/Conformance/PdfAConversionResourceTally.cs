// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;

namespace HyperPdfLibrary.Conformance;

/// <summary>Counts what the resource fixes found that stops a conversion.</summary>
[DebuggerDisplay("PdfAConversionResourceTally: {PostScript} PostScript, {BadHalftones} halftones")]
internal sealed class PdfAConversionResourceTally
{
    /// <summary>The threshold-array halftone type, which PDF/A allows with type 1.</summary>
    internal const int ThresholdHalftone = 5;

    /// <summary>Gets or sets the PostScript XObjects found.</summary>
    internal int PostScript { get; set; }

    /// <summary>Gets or sets the halftones PDF/A does not allow.</summary>
    internal int BadHalftones { get; set; }

    /// <summary>Gets or sets the blend modes the specification does not define.</summary>
    internal int UnknownBlendModes { get; set; }

    /// <summary>Gets or sets the images with a bit depth PDF/A does not allow.</summary>
    internal int BadBitDepths { get; set; }

    /// <summary>Records the changes and the problems.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="changed">The objects changed.</param>
    internal void Report(PdfAConversionSession session, int changed)
    {
        if (changed > 0)
        {
            session.Changed(
                "Removed printer-specific settings, image smoothing requests and links to outside content from images and graphics settings.",
                PdfARule.ExtGStateTransfer,
                PdfARule.ImageInterpolate,
                PdfARule.ImageAlternates,
                PdfARule.FormReference);
        }

        if (PostScript > 0)
        {
            session.RefuseIfBroken("The document has PostScript graphics, which PDF/A does not allow.", PdfARule.XObjectPostScript, PdfARule.FormPostScript, PdfARule.FormPostScriptPdfA1);
        }

        if (BadHalftones > 0)
        {
            session.Refuse("The document has halftone settings PDF/A does not allow.", PdfARule.HalftoneType, PdfARule.HalftoneTransfer);
        }

        if (UnknownBlendModes > 0)
        {
            session.RefuseIfBroken("The document uses a blend mode the PDF standard does not define.", PdfARule.BlendModeName, PdfARule.ExtGStateBlendModePdfA1);
        }

        if (BadBitDepths > 0)
        {
            session.RefuseIfBroken("Some images have a colour depth PDF/A does not allow.", PdfARule.ImageBitsPerComponent, PdfARule.ImageBitsPerComponentPdfA1);
        }
    }
}
