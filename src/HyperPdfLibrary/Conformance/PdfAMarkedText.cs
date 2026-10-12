// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Content;
using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Syntax;

namespace HyperPdfLibrary.Conformance;

/// <summary>Follows marked content in the PDF/A text walk to know whether shown text carries <c>/ActualText</c>.</summary>
internal static class PdfAMarkedText
{
    /// <summary>Applies <c>BMC</c>, <c>BDC</c> and <c>EMC</c>.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="frame">The stream's state.</param>
    /// <param name="reader">The reader, positioned after the operator.</param>
    /// <param name="op">The operator.</param>
    /// <returns><see langword="true"/> when the operator was one of these.</returns>
    internal static bool Apply(PdfATextWalkState state, PdfATextFrame frame, ref ContentReader reader, ContentOperator op)
    {
        switch (op)
        {
            case ContentOperator.BeginMarkedContent:
            {
                frame.Marked.Push(new(frame.HasActualText || ElementHasActualText(state, frame, frame.Mcid), frame.Mcid));
                return true;
            }

            case ContentOperator.BeginMarkedContentProperties:
            {
                var properties = Properties(state, frame, ref reader);
                var actualText = HasActualText(properties);
                state.PrivateUseActualText |= actualText && PdfAPrivateUse.Contains(properties!.GetText(KnownName.ActualText));
                var mcid = properties?.Get(KnownName.MCID) is { Kind: PdfKind.Integer } id ? (int)id.AsInteger() : frame.Mcid;
                frame.Marked.Push(new(frame.HasActualText || actualText || ElementHasActualText(state, frame, mcid), mcid));
                return true;
            }

            case ContentOperator.EndMarkedContent:
            {
                _ = frame.Marked.TryPop(out _);
                return true;
            }

            default:
            {
                return false;
            }
        }
    }

    /// <summary>Reads the property list of a <c>BDC</c>: inline, or named in the <c>/Properties</c> resources.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="frame">The stream's state.</param>
    /// <param name="reader">The reader, positioned after the operator.</param>
    /// <returns>The property list, or null.</returns>
    private static PdfDictionary? Properties(PdfATextWalkState state, PdfATextFrame frame, ref ContentReader reader)
    {
        var operand = reader.Operand(1);
        if (operand.Kind == ContentOperandKind.Dictionary)
        {
            return PdfParser.ForWindow(reader.Body(operand), 0, state.Store, state.Store.Names, null).ParseValue().AsDictionary();
        }

        return operand.Kind != ContentOperandKind.Name
            ? null
            : frame.Resources?.GetDictionary(KnownName.Properties)?.GetDictionary(operand.Name)
                ?? state.PageResources?.GetDictionary(KnownName.Properties)?.GetDictionary(operand.Name);
    }

    /// <summary>Determines whether the structure element that owns a marked-content id carries an <c>/ActualText</c> string.</summary>
    /// <param name="state">The walk.</param>
    /// <param name="frame">The stream's state.</param>
    /// <param name="mcid">The marked-content id, or -1.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool ElementHasActualText(PdfATextWalkState state, PdfATextFrame frame, int mcid) =>
        mcid >= 0 && frame.StructParents >= 0 && HasActualText(PdfAParentTree.Element(state.Validation, frame.StructParents, mcid));

    /// <summary>Determines whether a property list carries an <c>/ActualText</c> string.</summary>
    /// <param name="properties">The property list, or null.</param>
    /// <returns><see langword="true"/> when it does.</returns>
    private static bool HasActualText(PdfDictionary? properties) => properties?.Get(KnownName.ActualText).Kind == PdfKind.String;
}
