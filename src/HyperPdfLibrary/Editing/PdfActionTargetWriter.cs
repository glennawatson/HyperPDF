// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Document;
using HyperPdfLibrary.Navigation;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Editing;

/// <summary>Writes the destination or action that a bookmark or the document's opening action goes to (ISO 32000-2 12.3.2, 12.6.4).</summary>
internal static class PdfActionTargetWriter
{
    /// <summary>The names of the standard viewer commands a target may run (ISO 32000-2 12.6.4.12, Table 214).</summary>
    private static readonly string[] StandardActions = ["NextPage", "PrevPage", "FirstPage", "LastPage"];

    /// <summary>Writes the value a destination or action puts in an entry, replacing any destination or action there.</summary>
    /// <param name="document">The document.</param>
    /// <param name="item">The entry's dictionary.</param>
    /// <param name="target">The target.</param>
    /// <exception cref="ArgumentException">The target is not valid.</exception>
    /// <exception cref="InvalidOperationException">The target is of a kind this method does not know.</exception>
    internal static void Apply(PdfDocument document, PdfDictionary item, PdfBookmarkTarget target)
    {
        _ = item.Remove(KnownName.Dest);
        _ = item.Remove(KnownName.A);
        switch (target.Kind)
        {
            case PdfBookmarkTargetKind.None:
                {
                    break;
                }

            case PdfBookmarkTargetKind.Page:
                {
                    var destination = target.Destination ?? throw new ArgumentException("The bookmark target needs a destination.", nameof(target));
                    item.Set(KnownName.Dest, PdfValue.FromArray(PdfDestinationWriter.Create(document, destination)));
                    break;
                }

            case PdfBookmarkTargetKind.NamedDestination:
                {
                    item.Set(KnownName.Dest, PdfValue.FromString(PdfText.Encode(RequireText(target), item.Owner?.Version)));
                    break;
                }

            case PdfBookmarkTargetKind.Uri:
                {
                    item.Set(KnownName.A, PdfValue.FromDictionary(CreateUriAction(item.Owner, RequireText(target))));
                    break;
                }

            case PdfBookmarkTargetKind.NamedAction:
                {
                    item.Set(KnownName.A, PdfValue.FromDictionary(CreateNamedAction(item.Owner, RequireText(target))));
                    break;
                }

            default:
                {
                    throw new InvalidOperationException("The target was resolved before it reached the entry.");
                }
        }
    }

    /// <summary>Gets a target's text, which the kinds that need it must have.</summary>
    /// <param name="target">The target.</param>
    /// <returns>The text.</returns>
    /// <exception cref="ArgumentException">The target has no text.</exception>
    private static string RequireText(PdfBookmarkTarget target) =>
        string.IsNullOrEmpty(target.Text) ? throw new ArgumentException("The bookmark target needs a name, address or command.", nameof(target)) : target.Text;

    /// <summary>Builds a URI action.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="uri">The address, which must be 7-bit ASCII (ISO 32000-2 12.6.4.7).</param>
    /// <returns>The action.</returns>
    /// <exception cref="ArgumentException">The address has a character outside ASCII.</exception>
    private static PdfDictionary CreateUriAction(PdfObjectStore? store, string uri)
    {
        foreach (var character in uri)
        {
            if (character > sbyte.MaxValue)
            {
                throw new ArgumentException("A web address can hold only ASCII characters; write other characters as %XX escapes.", nameof(uri));
            }
        }

        var action = new PdfDictionary(store);
        action.Set(KnownName.S, PdfValue.FromName(KnownName.URI));
        action.Set(KnownName.URI, PdfValue.FromString(System.Text.Encoding.ASCII.GetBytes(uri)));
        return action;
    }

    /// <summary>Builds a named action.</summary>
    /// <param name="store">The document's objects.</param>
    /// <param name="name">The command.</param>
    /// <returns>The action.</returns>
    /// <exception cref="ArgumentException">The command is not one the standard defines for every viewer.</exception>
    private static PdfDictionary CreateNamedAction(PdfObjectStore? store, string name)
    {
        if (Array.IndexOf(StandardActions, name) < 0)
        {
            throw new ArgumentException("The viewer command must be NextPage, PrevPage, FirstPage or LastPage.", nameof(name));
        }

        var action = new PdfDictionary(store);
        action.Set(KnownName.S, PdfValue.FromName(KnownName.Named));
        action.Set(KnownName.N, PdfValue.FromName(store!.Names.Intern(name)));
        return action;
    }
}
