// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Requirements;
using HyperPdfLibrary.Tests.Document;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Tests.Requirements;

/// <summary>Builds, saves and reads back one-page documents with a <c>/Requirements</c> array for the requirement tests.</summary>
internal static class RequirementDocuments
{
    /// <summary>The requirement type of the links and outline.</summary>
    internal const string NavigationType = "Navigation";

    /// <summary>The penalty that marks a requirement as critical.</summary>
    internal const int Critical = 100;

    /// <summary>Gets an edit that makes a save write something.</summary>
    internal static PdfMetadataEdit TitleEdit => new() { Title = "Edited" };

    /// <summary>Opens a document with a requirements array.</summary>
    /// <param name="requirements">The array text, including its brackets.</param>
    /// <param name="extra">The objects numbered from 4.</param>
    /// <returns>The document.</returns>
    internal static PdfDocument Open(string requirements, params string[] extra) => StructureDocuments.Open($"/Version /2.0 /Requirements {requirements}", extra);

    /// <summary>Opens a document with requirements and extra catalog entries.</summary>
    /// <param name="requirements">The requirement dictionaries, without the array brackets.</param>
    /// <param name="catalogEntries">Extra catalog entries.</param>
    /// <param name="extra">The objects numbered from 4.</param>
    /// <returns>The document.</returns>
    internal static PdfDocument OpenWith(string requirements, string catalogEntries, params string[] extra) =>
        StructureDocuments.Open($"/Version /2.0 {catalogEntries} /Requirements [{requirements}]", extra);

    /// <summary>Builds an array of requirement dictionaries that all ask for the same type.</summary>
    /// <param name="count">The number of dictionaries.</param>
    /// <returns>The array text.</returns>
    internal static string Repeated(int count)
    {
        var builder = new StringBuilder("[");
        for (var i = 0; i < count; i++)
        {
            _ = builder.Append("<< /S /Navigation /Penalty 0 >> ");
        }

        return builder.Append(']').ToString();
    }

    /// <summary>Saves a document incrementally and reads the result.</summary>
    /// <param name="document">The edited document.</param>
    /// <param name="report">Receives what the save found.</param>
    /// <returns>The types of the requirements in the saved file, in order.</returns>
    internal static string[] SaveIncremental(PdfDocument document, PdfSaveReport report)
    {
        using var output = new MemoryStream();
        PdfIncrementalWriter.Save(document.Objects, output, report);
        return TypesOf(output.ToArray());
    }

    /// <summary>Reads the requirement types of a saved file.</summary>
    /// <param name="bytes">The file.</param>
    /// <returns>The types in order.</returns>
    internal static string[] TypesOf(byte[] bytes)
    {
        using var reopened = PdfDocumentReader.Open(bytes, null);
        return [.. PdfDocumentRequirements.GetRequirements(reopened).Requirements.Select(static requirement => requirement.Type)];
    }
}
