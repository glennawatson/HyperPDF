// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;
using HyperPdf.Core.Signatures.Signing;

namespace HyperPdf.ViewModels.Signing;

/// <summary>Describes in plain words what signing a document would do, so the user can check it before signing.</summary>
internal static class SigningPreviewText
{
    /// <summary>Gets the shortest description when the document cannot say what signing would do.</summary>
    internal static string Unavailable => UiText.Get("The document cannot say what signing would do.");

    /// <summary>Gets the kinds of certification the user can choose, in the order of <see cref="CertificationLevel"/>.</summary>
    internal static IReadOnlyList<string> CertificationChoices { get; } =
    UiText.CreateChoices(
        [
        "Approve: allow any later change",
        "Certify: allow no later change",
        "Certify: allow form filling and signing",
        "Certify: allow form filling, signing and comments"
    ]);

    /// <summary>Describes a preview.</summary>
    /// <param name="preview">The preview, or <see langword="null"/> when the document cannot be previewed.</param>
    /// <param name="timestampServer">The address of the timestamp server from Preferences; empty for none.</param>
    /// <returns>The description, one sentence per line.</returns>
    internal static string Describe(SigningPreview? preview, string timestampServer)
    {
        if (preview is null)
        {
            return Unavailable;
        }

        var text = new StringBuilder();
        var action = preview.IsNewField ? UiText.Get("Adds the signature field") : UiText.Get("Signs the empty field");
        var drawn = preview.IsVisible ? UiText.Get("drawn on the page") : UiText.Get("not drawn on the page");
        _ = text.AppendLine(
            UiText.Format("{0} {1} on page {2}, {3}.", action, preview.FieldName, preview.PageNumber, drawn))
            .AppendLine(UiText.Format("Signature type: {0}, digest {1}.", FormatName(preview.Format), DigestName(preview.Digest)));
        AppendOptional(text, UiText.Get("Reason"), preview.Reason);
        AppendOptional(text, UiText.Get("Location"), preview.Location);
        _ = text.AppendLine(UiText.Get("It covers the whole document, including your unsaved changes. A new copy is saved; this file is not changed."))
            .AppendLine(preview.LaterChanges);
        var hasServer = Uri.TryCreate(timestampServer, UriKind.Absolute, out _);
        if (hasServer)
        {
            _ = text.AppendLine(UiText.Format("A trusted timestamp is added from {0}. Only a hash of the signature is sent, never the document.", timestampServer));
        }
        else if (preview.TimestampRequired)
        {
            _ = text.AppendLine(UiText.Get("The signature field asks for a trusted timestamp. Type a timestamp server in Preferences."));
        }
        else
        {
            _ = text.AppendLine(UiText.Get("No timestamp is added. Type a timestamp server in Preferences to record a trusted time."));
        }

        foreach (var note in preview.Notes)
        {
            _ = text.AppendLine(note);
        }

        foreach (var problem in preview.Problems)
        {
            _ = text.AppendLine(UiText.Format("Cannot sign: {0}", problem));
        }

        return text.ToString().TrimEnd();
    }

    /// <summary>Gets the name of a signature format in plain words.</summary>
    /// <param name="format">The format.</param>
    /// <returns>The name.</returns>
    internal static string FormatName(SigningFormat format) => format switch
    {
        SigningFormat.CadesDetached => UiText.Get("PAdES (CAdES)"),
        _ => UiText.Get("standard (PKCS #7)"),
    };

    /// <summary>Gets the name of a digest.</summary>
    /// <param name="digest">The digest.</param>
    /// <returns>The name.</returns>
    internal static string DigestName(SigningDigest digest) => digest switch
    {
        SigningDigest.Sha384 => "SHA-384",
        SigningDigest.Sha512 => "SHA-512",
        SigningDigest.Sha3_256 => "SHA3-256",
        SigningDigest.Sha3_384 => "SHA3-384",
        SigningDigest.Sha3_512 => "SHA3-512",
        SigningDigest.Shake256 => "SHAKE256",
        _ => "SHA-256",
    };

    /// <summary>Adds a line for a value when there is one.</summary>
    /// <param name="text">The text.</param>
    /// <param name="label">The label.</param>
    /// <param name="value">The value.</param>
    private static void AppendOptional(StringBuilder text, string label, string value)
    {
        if (!string.IsNullOrEmpty(value))
        {
            _ = text.AppendLine(CultureInfo.CurrentCulture, $"{label}: {value}");
        }
    }
}
