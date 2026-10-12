// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>Checks the /Lang of a <c>BDC</c> property list, inline or named in the resources.</summary>
internal static class PdfAMarkedContentLanguage
{
    /// <summary>The context of failures in marked content.</summary>
    private const string Where = "marked content";

    /// <summary>Gets the language key.</summary>
    private static ReadOnlySpan<byte> LangKey => "Lang"u8;

    /// <summary>Checks the property list of the <c>BDC</c> just read.</summary>
    /// <param name="reader">The reader; its position is kept.</param>
    /// <param name="walk">The walk, holding the operands.</param>
    internal static void Check(ref PdfASyntaxReader reader, PdfASyntaxWalk walk)
    {
        if (walk.Operands.Count == 0)
        {
            return;
        }

        var properties = walk.Operands[^1];
        var position = reader.Position;
        reader.Position = properties.Start;
        reader.Next();
        if (properties.Kind == PdfASyntaxValue.Dictionary)
        {
            CheckInline(ref reader, walk);
        }
        else if (properties.Kind == PdfASyntaxValue.Name)
        {
            CheckNamed(walk, walk.Store.Names.Intern(reader.Value));
        }

        reader.Position = position;
    }

    /// <summary>Checks an inline property list's /Lang.</summary>
    /// <param name="reader">The reader, on the opening brackets.</param>
    /// <param name="walk">The walk.</param>
    private static void CheckInline(ref PdfASyntaxReader reader, PdfASyntaxWalk walk)
    {
        var scratch = walk.OperandFindings;
        reader.Next();
        while (reader.Kind == PdfASyntaxToken.Name)
        {
            var isLang = reader.Value.SequenceEqual(LangKey);
            reader.Next();
            if (isLang && reader.Kind is PdfASyntaxToken.LiteralString or PdfASyntaxToken.HexString)
            {
                PdfALanguageTags.Check(walk.Scan.Context, reader.Value, Where);
            }
            else if (PdfASyntaxValues.FromToken(ref reader, scratch, 1) == PdfASyntaxValue.Broken)
            {
                return;
            }

            reader.Next();
        }
    }

    /// <summary>Checks the /Lang of a property list named in the resources.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="name">The property list's resource name.</param>
    private static void CheckNamed(PdfASyntaxWalk walk, PdfName name)
    {
        var list = StoreReading.Resolve(walk.Store, walk.Resources?.GetDictionary(KnownName.Properties)?.GetRaw(name) ?? default).AsDictionary();
        if (list?.Get(KnownName.Lang) is { Kind: PdfKind.String } lang)
        {
            PdfALanguageTags.Check(walk.Scan.Context, lang.AsStringBytes(), Where);
        }
    }
}
