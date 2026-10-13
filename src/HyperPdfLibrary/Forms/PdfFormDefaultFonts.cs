// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using HyperPdfLibrary.Annotations;
using HyperPdfLibrary.Document;
using HyperPdfLibrary.Editing;
using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Forms;

/// <summary>
/// Adds fonts to the form's default resources (<c>/DR</c>, ISO 32000-2 12.7.3.1, Table 224). A field's default appearance
/// (<c>/DA</c>) names a font from here, and a document that claims PDF/A needs that font embedded (ISO 19005-2 6.2.11.4).
/// </summary>
public static class PdfFormDefaultFonts
{
    /// <summary>Embeds a TrueType font and adds it to the form's default resources, so a default appearance such as <c>/Name 12 Tf 0 g</c> can use it.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The resource name the default appearance uses, without the slash, such as <c>Body</c>.</param>
    /// <param name="font">The font.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The name is empty, or the form already has a font of that name.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public static void AddFont(PdfDocument document, string name, PdfTrueTypeFontInfo font) => AddFont(document, name, font, CancellationToken.None);

    /// <summary>Embeds a TrueType font and adds it to the form's default resources, so a default appearance such as <c>/Name 12 Tf 0 g</c> can use it.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The resource name the default appearance uses, without the slash, such as <c>Body</c>.</param>
    /// <param name="font">The font.</param>
    /// <param name="cancellationToken">Cancels the edit; the document is left unchanged.</param>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The name is empty, or the form already has a font of that name.</exception>
    /// <exception cref="OperationCanceledException">The token was cancelled.</exception>
    public static void AddFont(PdfDocument document, string name, PdfTrueTypeFontInfo font, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentException.ThrowIfNullOrWhiteSpace(name);
        ArgumentNullException.ThrowIfNull(font);
        cancellationToken.ThrowIfCancellationRequested();
        if (FormFieldFonts.FindInForm(document, $"/{name} 0 Tf") is not null)
        {
            throw new ArgumentException($"The form already has a font named '{name}' in its default resources.", nameof(name));
        }

        PdfDocumentEditing.RunEdit(document, "Add form font", PdfChangeKinds.Other, PdfCacheScopes.None, new FontRun(document, name, font), Apply);
    }

    /// <summary>Embeds a TrueType font and adds it to the form's default resources.</summary>
    /// <param name="document">The document.</param>
    /// <param name="name">The resource name the default appearance uses, without the slash.</param>
    /// <param name="font">The font.</param>
    /// <param name="cancellationToken">Cancels the edit.</param>
    /// <returns>A task that completes when the font is added.</returns>
    /// <exception cref="ArgumentNullException">An argument is <see langword="null"/>.</exception>
    /// <exception cref="ArgumentException">The name is empty, or the form already has a font of that name.</exception>
    public static ValueTask AddFontAsync(PdfDocument document, string name, PdfTrueTypeFontInfo font, CancellationToken cancellationToken)
    {
        AddFont(document, name, font, cancellationToken);
        return ValueTask.CompletedTask;
    }

    /// <summary>Adds the font inside the transaction.</summary>
    /// <param name="transaction">The transaction.</param>
    /// <param name="run">The font to add.</param>
    private static void Apply(PdfEditTransaction transaction, FontRun run)
    {
        var store = run.Document.Objects;
        var form = FormAcroForm.Ensure(transaction, store);
        var id = PdfEmbeddedFonts.AddTrueTypeFont(store, run.Font);
        var resources = form.GetDictionary(KnownName.DR)?.Clone() ?? new PdfDictionary(store);
        var fonts = resources.GetDictionary(KnownName.Font)?.Clone() ?? new PdfDictionary(store);
        fonts.Set(store.Names.Intern(run.Name), PdfValue.FromReference(id));
        resources.Set(KnownName.Font, PdfValue.FromDictionary(fonts));
        var copy = form.Clone();
        copy.Set(KnownName.DR, PdfValue.FromDictionary(resources));
        FormAcroForm.Store(transaction, store, copy);
    }

    /// <summary>The font to add.</summary>
    /// <param name="Document">The document.</param>
    /// <param name="Name">The resource name.</param>
    /// <param name="Font">The font.</param>
    private sealed record FontRun(PdfDocument Document, string Name, PdfTrueTypeFontInfo Font);
}
