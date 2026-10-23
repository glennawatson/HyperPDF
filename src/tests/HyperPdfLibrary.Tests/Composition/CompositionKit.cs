// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Composition;
using HyperPdfLibrary.Creation;
using HyperPdfLibrary.Fonts.Embedding;

namespace HyperPdfLibrary.Tests.Composition;

/// <summary>Makes the documents, styles and layouts the composition tests need.</summary>
internal static class CompositionKit
{
    /// <summary>The body size of the test styles, in points.</summary>
    internal const float BodySize = 11F;

    /// <summary>The margin of the test pages, in points.</summary>
    internal const float Margin = 36F;

    /// <summary>The width of the test pages, in points.</summary>
    internal const float PageWidth = 300F;

    /// <summary>Makes a document creator.</summary>
    /// <param name="tagged">Whether the document has a structure tree.</param>
    /// <returns>The creator.</returns>
    internal static PdfDocumentCreator CreateCreator(bool tagged) =>
        new(new() { Title = "Test", Author = "Tester", Tagged = tagged, Language = "en", Clock = new FixedClock() });

    /// <summary>Makes the default styles in a bundled sans typeface.</summary>
    /// <param name="creator">The document.</param>
    /// <returns>The styles.</returns>
    internal static PdfStyleSheet CreateStyles(PdfDocumentCreator creator) =>
        PdfStyleSheet.CreateDefault(PdfFontFamily.FromBundled(creator, PdfBundledFontFamily.Sans), BodySize);

    /// <summary>Makes a page setup with a small page and equal margins.</summary>
    /// <param name="height">The page height in points.</param>
    /// <returns>The setup.</returns>
    internal static PdfPageSetup CreateSetup(float height) =>
        new() { Size = new(PageWidth, height), MarginTop = Margin, MarginBottom = Margin, MarginLeft = Margin, MarginRight = Margin };

    /// <summary>Lays a composition out without writing it.</summary>
    /// <param name="composition">The composition.</param>
    /// <returns>The layout.</returns>
    internal static DocumentLayout Layout(PdfComposition composition)
    {
        var env = new LayoutEnv(composition, DateTimeOffset.UnixEpoch, false);
        var layout = new DocumentLayout(env, CancellationToken.None);
        layout.Run();
        return layout;
    }

    /// <summary>Gets the text commands of a page, in drawing order.</summary>
    /// <param name="plan">The page.</param>
    /// <returns>The commands.</returns>
    internal static List<TextCommand> TextOf(PagePlan plan)
    {
        var texts = new List<TextCommand>();
        foreach (var command in plan.Commands)
        {
            if (command is TextCommand text)
            {
                texts.Add(text);
            }
        }

        return texts;
    }

    /// <summary>A clock that always says the same time.</summary>
    private sealed class FixedClock : TimeProvider
    {
        /// <inheritdoc/>
        public override DateTimeOffset GetUtcNow() => DateTimeOffset.UnixEpoch;
    }
}
