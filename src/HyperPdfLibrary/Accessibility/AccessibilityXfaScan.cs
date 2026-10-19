// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Xml;
using HyperPdfLibrary.Compat;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Xfa;

namespace HyperPdfLibrary.Accessibility;

/// <summary>Finds dynamic XFA forms, which ISO 14289-1 clause 7.15 does not allow.</summary>
internal static class AccessibilityXfaScan
{
    /// <summary>The element that says whether a form is dynamic; a conforming reader compares its value to <c>required</c>.</summary>
    private const string DynamicRender = "dynamicRender";

    /// <summary>The value of that element that makes the form dynamic.</summary>
    private const string Required = "required";

    /// <summary>The packet that holds the configuration of a form written as an array of packets.</summary>
    private const string ConfigPacket = "config";

    /// <summary>The packet name of a form written as one stream.</summary>
    private const string SingleStreamPacket = "xdp";

    /// <summary>The reader settings: forward only, no DTDs, no resolver, bounded size.</summary>
    private static readonly XmlReaderSettings Settings = new()
    {
        DtdProcessing = DtdProcessing.Prohibit,
        XmlResolver = null,
        IgnoreComments = true,
        IgnoreProcessingInstructions = true,
        IgnoreWhitespace = true,
        MaxCharactersInDocument = PdfLimits.MaxDecodedLength,
    };

    /// <summary>Reports a form whose configuration asks for dynamic rendering.</summary>
    /// <param name="document">The document.</param>
    /// <param name="part">The claimed PDF/UA part, or <see langword="null"/>.</param>
    /// <param name="findings">Receives the finding.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    internal static void Run(PdfDocument document, int? part, AccessibilityFindings findings, CancellationToken cancellationToken)
    {
        cancellationToken.ThrowIfCancellationRequested();
        if (part != PdfUaClaim.Part1 || !PdfDocumentXfa.HasXfa(document))
        {
            return;
        }

        var xfa = PdfDocumentXfa.GetXfa(document);
        var packet = xfa?.Find(ConfigPacket) ?? xfa?.Find(SingleStreamPacket);
        if (packet is not null && AsksForDynamicRender(packet, cancellationToken))
        {
            AccessibilityFindingCollection.Add(findings, PdfAccessibilityCode.DynamicXfaForm);
        }
    }

    /// <summary>Reads a packet for a <c>dynamicRender</c> element with the value <c>required</c>; damaged XML ends the read.</summary>
    /// <param name="packet">The packet.</param>
    /// <param name="cancellationToken">Cancels the read.</param>
    /// <returns><see langword="true"/> when the form is dynamic.</returns>
    private static bool AsksForDynamicRender(PdfXfaPacket packet, CancellationToken cancellationToken)
    {
        try
        {
            using var stream = MemoryStreams.OpenRead(packet.Data.AsMemory());
            using var reader = XmlReader.Create(stream, Settings);
            while (reader.Read())
            {
                cancellationToken.ThrowIfCancellationRequested();
                if (reader.NodeType == XmlNodeType.Element
                    && string.Equals(reader.LocalName, DynamicRender, StringComparison.Ordinal)
                    && string.Equals(reader.ReadElementContentAsString().Trim(), Required, StringComparison.Ordinal))
                {
                    return true;
                }
            }
        }
        catch (XmlException)
        {
            // A damaged packet cannot say the form is dynamic; the file is still read leniently.
        }

        return false;
    }
}
