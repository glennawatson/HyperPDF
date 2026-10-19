// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Xml;
using HyperPdfLibrary.Conformance;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Metadata;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Pdf20;

/// <summary>
/// Moves the document information into the catalog's XMP metadata (ISO 32000-2 14.3.3). PDF 2.0 deprecates every
/// information entry except <c>/CreationDate</c> and <c>/ModDate</c>. Title, author, subject, keywords, creator, producer
/// and trapping state go to their XMP properties; any other key goes to the <c>pdfx</c> namespace under its own name.
/// When the XMP already holds a different value the XMP value stays, and the summary names the value not carried over.
/// The two dates stay in <c>/Info</c> and are made to match <c>xmp:CreateDate</c> and <c>xmp:ModifyDate</c>.
/// </summary>
internal static class Pdf20ConversionInfo
{
    /// <summary>The clause that deprecates the information dictionary.</summary>
    private const string InfoClause = "14.3.3";

    /// <summary>The clause that defines metadata streams.</summary>
    private const string XmpClause = "14.3.2";

    /// <summary>The prefix of the namespace custom entries go to.</summary>
    private const string CustomPrefix = "pdfx";

    /// <summary>The XMP property that names the file's media type.</summary>
    private static readonly XmpProperty Format = new("dc", XmpMetadata.DublinCoreNamespace, "format", XmpShape.Simple);

    /// <summary>Moves the document information into XMP.</summary>
    /// <param name="session">The conversion.</param>
    internal static void Apply(Pdf20ConversionSession session)
    {
        var store = session.Store;
        var reference = store.Trailer.GetRaw(KnownName.Info);
        if (StoreReading.Resolve(store, reference).AsDictionary() is not { } original)
        {
            return;
        }

        var xmp = Pdf20ConversionXmp.Read(session.Document);
        var plan = new Pdf20InfoPlan(original.Clone());
        MoveStandard(session, original, xmp, plan);
        MoveCustom(session, original, xmp, plan);
        Pdf20ConversionInfoDates.Reconcile(plan, xmp);
        if (plan.Blocked || !WriteXmp(session, xmp, plan))
        {
            return;
        }

        WriteInfo(store, reference, plan.Info);
        Report(session, plan);
    }

    /// <summary>Routes one value to its XMP property.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="property">The XMP property.</param>
    /// <param name="label">The entry's name in plain words.</param>
    /// <param name="value">The information value, or <see langword="null"/>.</param>
    /// <param name="existing">The value the XMP holds, or <see langword="null"/>.</param>
    /// <returns>What was done with the value.</returns>
    internal static Pdf20InfoRoute Route(Pdf20InfoPlan plan, XmpProperty property, string label, string? value, string? existing)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return Pdf20InfoRoute.Skipped;
        }

        if (!PdfAConversionXmp.CanHold(value))
        {
            plan.Left.Add($"The document information {label} holds characters that XMP cannot store, so it was left out.");
            return Pdf20InfoRoute.Left;
        }

        if (string.IsNullOrWhiteSpace(existing))
        {
            plan.Changes.Add(new(property, value));
            return Pdf20InfoRoute.Moved;
        }

        if (Pdf20InfoText.Same(existing, value))
        {
            return Pdf20InfoRoute.Matched;
        }

        plan.Left.Add(
            $"The XMP metadata already has a different {label} ({Pdf20InfoText.Quote(existing)}), so it was kept. "
            + $"The document information {label} ({Pdf20InfoText.Quote(value)}) was not carried over.");
        return Pdf20InfoRoute.Left;
    }

    /// <summary>Moves the standard entries ISO 32000-2 deprecates.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="original">The information dictionary.</param>
    /// <param name="xmp">The catalog's XMP.</param>
    /// <param name="plan">The plan.</param>
    private static void MoveStandard(Pdf20ConversionSession session, PdfDictionary original, XmpMetadata xmp, Pdf20InfoPlan plan)
    {
        var names = session.Store.Names;
        foreach (var mapping in Pdf20InfoMapping.All)
        {
            if (!original.ContainsKey(mapping.Key))
            {
                continue;
            }

            _ = plan.Info.Remove(mapping.Key);
            var value = original.Get(mapping.Key);
            if (!TryReadStandard(names, mapping, value, out var text))
            {
                plan.Blocked = true;
                session.Refuse(InfoClause, $"The document information {mapping.Label} is a list or dictionary, which XMP cannot hold, so it cannot be moved.");
                continue;
            }

            Count(plan, Route(plan, mapping.Property, mapping.Label, text, mapping.Existing(xmp)), mapping.Label);
        }
    }

    /// <summary>Reads a standard entry's text.</summary>
    /// <param name="names">The name table.</param>
    /// <param name="mapping">The entry.</param>
    /// <param name="value">The entry's value.</param>
    /// <param name="text">Receives the text.</param>
    /// <returns><see langword="false"/> when the value cannot be read as text.</returns>
    private static bool TryReadStandard(PdfNameTable names, Pdf20InfoMapping mapping, PdfValue value, out string? text)
    {
        if (mapping.Key != KnownName.Trapped)
        {
            return Pdf20InfoText.TryRead(names, value, out text);
        }

        text = Pdf20InfoText.ReadTrapped(names, value);
        return true;
    }

    /// <summary>Moves every other entry to the pdfx namespace.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="original">The information dictionary.</param>
    /// <param name="xmp">The catalog's XMP.</param>
    /// <param name="plan">The plan.</param>
    private static void MoveCustom(Pdf20ConversionSession session, PdfDictionary original, XmpMetadata xmp, Pdf20InfoPlan plan)
    {
        var names = session.Store.Names;
        for (var i = 0; i < original.Count; i++)
        {
            var key = original.GetKeyAt(i);
            if (Pdf20InfoMapping.IsKept(key))
            {
                continue;
            }

            var name = names.GetString(key);
            _ = plan.Info.Remove(key);
            if (!Pdf20InfoText.TryRead(names, original.Get(key), out var text))
            {
                plan.Blocked = true;
                session.Refuse(InfoClause, $"The custom document information entry \"{name}\" is a list or dictionary, which XMP cannot hold, so it cannot be moved.");
                continue;
            }

            var local = XmlConvert.EncodeLocalName(name);
            var property = new XmpProperty(CustomPrefix, XmpMetadata.PdfXNamespace, local, XmpShape.Simple);
            var outcome = Route(plan, property, $"custom entry \"{name}\"", text, xmp.GetValue(XmpMetadata.PdfXNamespace, local));
            plan.CustomMoved += outcome == Pdf20InfoRoute.Moved ? 1 : 0;
            plan.CustomMatched += outcome == Pdf20InfoRoute.Matched ? 1 : 0;
        }
    }

    /// <summary>Records where a standard entry went.</summary>
    /// <param name="plan">The plan.</param>
    /// <param name="route">What was done with it.</param>
    /// <param name="label">The entry's name in plain words.</param>
    private static void Count(Pdf20InfoPlan plan, Pdf20InfoRoute route, string label)
    {
        if (route == Pdf20InfoRoute.Moved)
        {
            plan.Moved.Add(label);
        }
        else if (route == Pdf20InfoRoute.Matched)
        {
            plan.Matched.Add(label);
        }
    }

    /// <summary>Writes the planned changes to the XMP packet.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="xmp">The catalog's XMP before the changes.</param>
    /// <param name="plan">The plan.</param>
    /// <returns><see langword="false"/> when the packet could not be edited.</returns>
    private static bool WriteXmp(Pdf20ConversionSession session, XmpMetadata xmp, Pdf20InfoPlan plan)
    {
        if (plan.Changes.Count == 0)
        {
            return true;
        }

        if (xmp.RawPacket.Length == 0)
        {
            plan.Changes.Add(new(Format, "application/pdf"));
        }

        var result = Pdf20ConversionXmp.Write(session.Document, plan.Changes);
        if (result == XmpWriteResult.Failed)
        {
            session.Refuse(XmpClause, "The document's XMP metadata could not be edited safely, so the document information could not be moved into it.");
            return false;
        }

        plan.CreatedXmp = result == XmpWriteResult.Created;
        return true;
    }

    /// <summary>Writes the information dictionary back as an indirect object, or removes it when nothing is left.</summary>
    /// <param name="store">The working copy's objects.</param>
    /// <param name="reference">The trailer's /Info value.</param>
    /// <param name="info">The information dictionary after the moves.</param>
    private static void WriteInfo(PdfObjectStore store, PdfValue reference, PdfDictionary info)
    {
        if (info.Count == 0)
        {
            _ = store.Trailer.Remove(KnownName.Info);
        }
        else if (reference.IsReference)
        {
            StoreEditing.Replace(store, reference.AsReference(), PdfValue.FromDictionary(info));
        }
        else
        {
            store.Trailer.Set(KnownName.Info, PdfValue.FromReference(StoreEditing.Add(store, PdfValue.FromDictionary(info))));
        }
    }

    /// <summary>Tells the user what moved, what was already there, and what was left out.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="plan">The plan.</param>
    private static void Report(Pdf20ConversionSession session, Pdf20InfoPlan plan)
    {
        if (plan.Moved.Count > 0)
        {
            session.Changed(
                InfoClause,
                $"Moved the document information {Pdf20InfoText.Join(plan.Moved)} to the XMP metadata, as PDF 2.0 keeps only the two dates in the document information.");
        }

        if (plan.Matched.Count > 0)
        {
            session.Changed(InfoClause, $"Removed the document information {Pdf20InfoText.Join(plan.Matched)}, as the XMP metadata already holds the same value.");
        }

        ReportCustom(session, plan);
        if (plan.DatesAdded.Count > 0)
        {
            session.Changed(InfoClause, $"Copied the document information {Pdf20InfoText.Join(plan.DatesAdded)} to the XMP metadata, so the two match.");
        }

        if (plan.CreatedXmp)
        {
            session.Changed(XmpClause, "Created XMP metadata for the document, as it had none, to hold the moved values.");
        }

        foreach (var sentence in plan.Left)
        {
            session.Changed(InfoClause, sentence);
        }
    }

    /// <summary>Tells the user about the custom entries.</summary>
    /// <param name="session">The conversion.</param>
    /// <param name="plan">The plan.</param>
    private static void ReportCustom(Pdf20ConversionSession session, Pdf20InfoPlan plan)
    {
        if (plan.CustomMoved > 0)
        {
            session.Changed(
                InfoClause,
                string.Create(CultureInfo.InvariantCulture, $"Moved {plan.CustomMoved} custom document information {Entries(plan.CustomMoved)} to the XMP metadata, in the pdfx namespace."));
        }

        if (plan.CustomMatched > 0)
        {
            session.Changed(
                InfoClause,
                string.Create(CultureInfo.InvariantCulture, $"Removed {plan.CustomMatched} custom document information {Entries(plan.CustomMatched)} that the XMP metadata already holds."));
        }
    }

    /// <summary>Gets the word for a count of entries.</summary>
    /// <param name="count">The count.</param>
    /// <returns>Entry or entries.</returns>
    private static string Entries(int count) => count == 1 ? "entry" : "entries";
}
