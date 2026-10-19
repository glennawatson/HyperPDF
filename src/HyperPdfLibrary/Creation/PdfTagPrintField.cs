// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;
using HyperPdfLibrary.Structure.Tagged;
using HyperPdfLibrary.Writing;

namespace HyperPdfLibrary.Creation;

/// <summary>Validates and writes PrintField attributes (ISO 32000-1 Table 348 and ISO 32000-2 Table 383).</summary>
internal static class PdfTagPrintField
{
    /// <summary>Rejects unknown values and attributes incompatible with the structure type or output version.</summary>
    /// <param name="sink">The output document.</param>
    /// <param name="type">The field's resolved structure type.</param>
    /// <param name="field">The requested field attributes.</param>
    /// <returns>Whether the output supports PDF 2.0 text strings and attribute spellings.</returns>
    /// <exception cref="ArgumentException">A value, structure type or output version is incompatible.</exception>
    internal static bool Validate(IPdfObjectSink sink, PdfStructureType type, PdfPrintFieldAttributes field)
    {
        if (type != PdfStructureType.Form)
        {
            throw new ArgumentException("PrintField attributes require a Form structure element.", nameof(field));
        }

        ValidateRole(field);
        ValidateState(field);
        var version = OutputVersion(sink);
        if (version == PdfHeaderVersion.Pdf14)
        {
            throw new ArgumentException("PrintField attributes require PDF 1.7 or later output.", nameof(field));
        }

        if (field.Role == "lb" && version != PdfHeaderVersion.Pdf20)
        {
            throw new ArgumentException("The listbox PrintField role requires PDF 2.0 output.", nameof(field));
        }

        return version == PdfHeaderVersion.Pdf20;
    }

    /// <summary>Creates a separate PrintField owner with only the requested entries.</summary>
    /// <param name="sink">The document's object and name owner.</param>
    /// <param name="field">The field attributes.</param>
    /// <returns>The attribute dictionary.</returns>
    /// <exception cref="ArgumentException">The attributes are incompatible with the current output version.</exception>
    internal static PdfDictionary Create(IPdfObjectSink sink, PdfPrintFieldAttributes field)
    {
        // The builder's version can change after AddElement, so validate against the version being written.
        var pdf20 = Validate(sink, PdfStructureType.Form, field);
        var count = 1 + (field.Role is null ? 0 : 1) + (field.Checked is null ? 0 : 1) + (field.Description is null ? 0 : 1);
        var attributes = new PdfDictionary(sink.Owner, count);
        attributes.Set(KnownName.O, PdfValue.FromName(sink.Names.Intern("PrintField"u8)));
        if (field.Role is { } role)
        {
            attributes.Set(sink.Names.Intern("Role"u8), PdfValue.FromName(sink.Names.Intern(RoleName(role))));
        }

        if (field.Checked is { } state)
        {
            attributes.Set(sink.Names.Intern(pdf20 ? "Checked"u8 : "checked"u8), PdfValue.FromName(sink.Names.Intern(StateName(state))));
        }

        if (field.Description is { } description)
        {
            attributes.Set(sink.Names.Intern("Desc"u8), PdfValue.FromString(PdfText.Encode(description, pdf20)));
        }

        return attributes;
    }

    /// <summary>Rejects nonstandard role names without changing lenient source reading.</summary>
    /// <param name="field">The requested attributes.</param>
    /// <exception cref="ArgumentException">The role is undefined.</exception>
    private static void ValidateRole(PdfPrintFieldAttributes field)
    {
        if (field.Role is not (null or "rb" or "cb" or "pb" or "tv" or "lb"))
        {
            throw new ArgumentException("The PrintField role is undefined.", nameof(field));
        }
    }

    /// <summary>Rejects unknown states and state attributes on explicitly incompatible roles.</summary>
    /// <param name="field">The requested attributes.</param>
    /// <exception cref="ArgumentException">The checked state is undefined or incompatible with the role.</exception>
    private static void ValidateState(PdfPrintFieldAttributes field)
    {
        if (field.Checked is not (null or "on" or "off" or "neutral"))
        {
            throw new ArgumentException("The PrintField checked state is undefined.", nameof(field));
        }

        if (field.Checked is not null && field.Role is "pb" or "tv" or "lb")
        {
            throw new ArgumentException("A checked state describes a radio button or checkbox, not this field role.", nameof(field));
        }
    }

    /// <summary>Gets the supported output version, including a catalog version upgrade on an edited document.</summary>
    /// <param name="sink">The output document.</param>
    /// <returns>The supported output version; unknown sinks cannot assert PrintField support.</returns>
    private static PdfHeaderVersion OutputVersion(IPdfObjectSink sink)
    {
        PdfHeaderVersion header;
        PdfDictionary catalog;
        if (sink is PdfDocumentBuilder builder)
        {
            header = builder.Version;
            catalog = builder.CatalogEntries;
        }
        else if (sink.Owner is { } store)
        {
            header = store.Version switch
            {
                "2.0" => PdfHeaderVersion.Pdf20,
                "1.7" => PdfHeaderVersion.Pdf17,
                _ => PdfHeaderVersion.Pdf14,
            };
            catalog = store.Catalog;
        }
        else
        {
            return PdfHeaderVersion.Pdf14;
        }

        var version = catalog.Get(KnownName.Version);
        if (header == PdfHeaderVersion.Pdf20 || (version.Kind == PdfKind.Name && sink.Names.NameEquals(version.AsName(), "2.0"u8)))
        {
            return PdfHeaderVersion.Pdf20;
        }

        return header == PdfHeaderVersion.Pdf17 || (version.Kind == PdfKind.Name && sink.Names.NameEquals(version.AsName(), "1.7"u8))
            ? PdfHeaderVersion.Pdf17
            : PdfHeaderVersion.Pdf14;
    }

    /// <summary>Returns the validated role's fixed byte spelling.</summary>
    /// <param name="role">The validated role.</param>
    /// <returns>The name bytes.</returns>
    private static ReadOnlySpan<byte> RoleName(string role) => role switch
    {
        "rb" => "rb"u8,
        "cb" => "cb"u8,
        "pb" => "pb"u8,
        "tv" => "tv"u8,
        _ => "lb"u8,
    };

    /// <summary>Returns the validated checked state's fixed byte spelling.</summary>
    /// <param name="state">The validated checked state.</param>
    /// <returns>The name bytes.</returns>
    private static ReadOnlySpan<byte> StateName(string state) => state switch
    {
        "on" => "on"u8,
        "off" => "off"u8,
        _ => "neutral"u8,
    };
}
