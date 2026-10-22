// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdfLibrary.Objects;

namespace HyperPdfLibrary.Conformance;

/// <summary>
/// Reads a content stream's operators and checks the operands veraPDF links to the implementation limits: shown strings,
/// font and tag names, path and text numbers, inline property lists and inline image filters (ISO 19005-2, 6.1.13 and
/// 6.1.10). A stream veraPDF cannot read counts for nothing.
/// </summary>
internal static class PdfASyntaxContent
{
    /// <summary>The context of failures in content streams.</summary>
    private const string Where = "content stream";

    /// <summary>The operands <c>Tf</c>, <c>Td</c> and the dash operator take.</summary>
    private const int PairOperands = 2;

    /// <summary>Reads one content stream.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="work">The stream.</param>
    internal static void Check(PdfASyntaxWalk walk, PdfASyntaxWork work)
    {
        var data = Decode(work.Parts, walk.Scan.Context.Streams, walk.Scan.CancellationToken);
        walk.Resources = work.Resources;
        walk.Font = null;
        walk.SavedFonts.Clear();
        ClearOperands(walk);
        var reader = new PdfASyntaxReader(data, 0, walk.Scan.Buffer);
        var findings = walk.Scan.Findings;
        if (ReadOperators(ref reader, walk))
        {
            findings.Commit(walk.Scan.Context, Where);
        }
        else
        {
            findings.Discard();
        }

        ClearOperands(walk);
    }

    /// <summary>Decodes a stream's parts and joins them, as veraPDF reads a page's contents array.</summary>
    /// <param name="parts">The parts.</param>
    /// <param name="streams">The decoded streams the validation shares.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The bytes; readers must not change them.</returns>
    private static byte[] Decode(PdfStream[] parts, PdfAStreamDecodes streams, CancellationToken cancellationToken)
    {
        if (parts.Length == 1)
        {
            return DecodePart(parts[0], streams, cancellationToken);
        }

        var joined = new List<byte>();
        foreach (var part in parts)
        {
            joined.AddRange(DecodePart(part, streams, cancellationToken));
        }

        return [.. joined];
    }

    /// <summary>Decodes one part, from the shared data when another rule area decoded it.</summary>
    /// <param name="part">The part.</param>
    /// <param name="streams">The decoded streams the validation shares.</param>
    /// <param name="cancellationToken">Cancels decoding.</param>
    /// <returns>The bytes.</returns>
    private static byte[] DecodePart(PdfStream part, PdfAStreamDecodes streams, CancellationToken cancellationToken) =>
        // A stream that failed to decode throws again from DecodeToArray, as it did before the data was shared.
        streams.TryGet(part, cancellationToken, out var data) ? data : part.DecodeToArray(cancellationToken);

    /// <summary>Reads tokens until the end of the stream.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="walk">The walk.</param>
    /// <returns><see langword="false"/> when the stream holds syntax veraPDF cannot read.</returns>
    private static bool ReadOperators(ref PdfASyntaxReader reader, PdfASyntaxWalk walk)
    {
        var readable = true;
        var ended = false;
        while (readable && !ended)
        {
            reader.SkipSpaces(true);
            var start = reader.Position;
            reader.Next();
            switch (reader.Kind)
            {
                case PdfASyntaxToken.End:
                {
                    ended = true;
                    break;
                }

                case PdfASyntaxToken.Keyword when !IsValueWord(ref reader):
                {
                    readable = RunOperator(ref reader, walk);
                    break;
                }

                default:
                {
                    readable = AddOperand(ref reader, walk, start);
                    break;
                }
            }
        }

        return readable;
    }

    /// <summary>Determines whether a word is a value rather than an operator.</summary>
    /// <param name="reader">The reader, on the word.</param>
    /// <returns><see langword="true"/> for null, true and false.</returns>
    private static bool IsValueWord(ref PdfASyntaxReader reader) => reader.IsWord("null"u8) || reader.IsWord("true"u8) || reader.IsWord("false"u8);

    /// <summary>Reads an operand and keeps it with the rules it breaks.</summary>
    /// <param name="reader">The reader, on the operand's first token.</param>
    /// <param name="walk">The walk.</param>
    /// <param name="start">Where the token starts.</param>
    /// <returns><see langword="false"/> when the operand cannot be read.</returns>
    private static bool AddOperand(ref PdfASyntaxReader reader, PdfASyntaxWalk walk, int start)
    {
        var findings = walk.OperandFindings;
        var first = findings.Count;
        var kind = reader.Kind == PdfASyntaxToken.Integer ? ReadNumber(ref reader, findings) : PdfASyntaxValues.FromToken(ref reader, findings, 0);
        if (kind == PdfASyntaxValue.Broken)
        {
            return false;
        }

        walk.Operands.Add(new(kind, first, findings.Count - first, start));
        return true;
    }

    /// <summary>Reads an integer operand on its own: content streams hold no references.</summary>
    /// <param name="reader">The reader, on the integer.</param>
    /// <param name="findings">Receives the broken rules.</param>
    /// <returns>The kind of value.</returns>
    private static PdfASyntaxValue ReadNumber(ref PdfASyntaxReader reader, PdfAObjectFindings findings)
    {
        findings.CheckInteger(reader.Integer);
        return PdfASyntaxValue.Integer;
    }

    /// <summary>Applies an operator: checks its linked operands and follows the resources it uses.</summary>
    /// <param name="reader">The reader, on the operator.</param>
    /// <param name="walk">The walk.</param>
    /// <returns><see langword="false"/> when an inline image cannot be read.</returns>
    private static bool RunOperator(ref PdfASyntaxReader reader, PdfASyntaxWalk walk)
    {
        var word = reader.Value;
        if (word.SequenceEqual("BI"u8))
        {
            ClearOperands(walk);
            return PdfAInlineImageSyntax.Read(ref reader, walk.Scan.Findings, walk.OperandFindings);
        }

        Link(walk, PdfASyntaxOperators.LinkOf(word));
        Follow(ref reader, walk, word);
        ClearOperands(walk);
        return true;
    }

    /// <summary>Copies the rules of the operands an operator links to the stream's findings.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="link">The operands linked.</param>
    private static void Link(PdfASyntaxWalk walk, PdfASyntaxLink link)
    {
        switch (link)
        {
            case PdfASyntaxLink.LastNumber:
            {
                CopyIf(walk, 1, IsNumber);
                break;
            }

            case PdfASyntaxLink.LastTwoNumbers:
            {
                CopyIf(walk, 1, IsNumber);
                CopyIf(walk, PairOperands, IsNumber);
                break;
            }

            case PdfASyntaxLink.LastInteger:
            {
                CopyIf(walk, 1, static kind => kind == PdfASyntaxValue.Integer);
                break;
            }

            case PdfASyntaxLink.AllNumbers:
            {
                CopyNumbers(walk);
                break;
            }

            default:
            {
                LinkValues(walk, link);
                break;
            }
        }
    }

    /// <summary>Copies the rules of the string, array, name and dictionary operands an operator links.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="link">The operands linked.</param>
    private static void LinkValues(PdfASyntaxWalk walk, PdfASyntaxLink link)
    {
        switch (link)
        {
            case PdfASyntaxLink.Dash:
            {
                CopyIf(walk, PairOperands, static kind => kind == PdfASyntaxValue.Array);
                CopyIf(walk, 1, IsNumber);
                break;
            }

            case PdfASyntaxLink.LastString:
            {
                CopyIf(walk, 1, static kind => kind == PdfASyntaxValue.String);
                break;
            }

            case PdfASyntaxLink.LastArray:
            {
                CopyIf(walk, 1, static kind => kind == PdfASyntaxValue.Array);
                break;
            }

            case PdfASyntaxLink.Font:
            {
                CopyIf(walk, 1, IsNumber);
                CopyIf(walk, PairOperands, static kind => kind == PdfASyntaxValue.Name);
                break;
            }

            case PdfASyntaxLink.LastName:
            {
                CopyIf(walk, 1, static kind => kind == PdfASyntaxValue.Name);
                break;
            }

            case PdfASyntaxLink.TagAndProperties:
            {
                CopyIf(walk, PairOperands, static kind => kind == PdfASyntaxValue.Name);
                CopyIf(walk, 1, static kind => kind == PdfASyntaxValue.Dictionary);
                break;
            }

            default:
            {
                // The operator's operands are not checked.
                break;
            }
        }
    }

    /// <summary>Copies an operand's rules when it is of the expected kind.</summary>
    /// <param name="walk">The walk.</param>
    /// <param name="fromEnd">The operand's position from the end: 1 for the last.</param>
    /// <param name="expected">Tests the operand's kind.</param>
    private static void CopyIf(PdfASyntaxWalk walk, int fromEnd, Func<PdfASyntaxValue, bool> expected)
    {
        var operands = walk.Operands;
        if (operands.Count < fromEnd)
        {
            return;
        }

        var operand = operands[^fromEnd];
        if (expected(operand.Kind))
        {
            walk.OperandFindings.CopyTo(walk.Scan.Findings, operand.FirstRule, operand.RuleCount, false);
        }
    }

    /// <summary>Copies the rules of every number operand and the numbers inside array operands.</summary>
    /// <param name="walk">The walk.</param>
    private static void CopyNumbers(PdfASyntaxWalk walk)
    {
        foreach (var operand in walk.Operands)
        {
            if (IsNumber(operand.Kind) || operand.Kind == PdfASyntaxValue.Array)
            {
                walk.OperandFindings.CopyTo(walk.Scan.Findings, operand.FirstRule, operand.RuleCount, operand.Kind == PdfASyntaxValue.Array);
            }
        }
    }

    /// <summary>Follows the resources an operator uses: forms, tiling patterns, and Type 3 fonts that show text.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="walk">The walk.</param>
    /// <param name="word">The operator.</param>
    private static void Follow(ref PdfASyntaxReader reader, PdfASyntaxWalk walk, ReadOnlySpan<byte> word)
    {
        if (word.SequenceEqual("q"u8))
        {
            walk.SavedFonts.Push(walk.Font);
        }
        else if (word.SequenceEqual("Q"u8))
        {
            walk.Font = walk.SavedFonts.TryPop(out var saved) ? saved : walk.Font;
        }
        else if (word.SequenceEqual("Tf"u8))
        {
            walk.Font = LastName(ref reader, walk, PairOperands) is { } font
                ? StoreReading.Resolve(walk.Store, walk.Resources?.GetDictionary(KnownName.Font)?.GetRaw(font) ?? default).AsDictionary()
                : null;
        }
        else if (word.SequenceEqual("BDC"u8))
        {
            PdfAMarkedContentLanguage.Check(ref reader, walk);
        }
        else
        {
            FollowResource(ref reader, walk, word);
        }
    }

    /// <summary>Queues the form, tiling pattern or Type 3 glyphs an operator draws with.</summary>
    /// <param name="reader">The reader.</param>
    /// <param name="walk">The walk.</param>
    /// <param name="word">The operator.</param>
    private static void FollowResource(ref PdfASyntaxReader reader, PdfASyntaxWalk walk, ReadOnlySpan<byte> word)
    {
        if (word.SequenceEqual("Do"u8))
        {
            if (LastName(ref reader, walk, 1) is { } xobject)
            {
                PdfASyntaxStreams.QueueXObject(walk, xobject);
            }
        }
        else if (word.SequenceEqual("scn"u8) || word.SequenceEqual("SCN"u8))
        {
            if (LastName(ref reader, walk, 1) is { } pattern)
            {
                PdfASyntaxStreams.QueuePattern(walk, pattern);
            }
        }
        else if (PdfASyntaxOperators.LinkOf(word) is PdfASyntaxLink.LastString or PdfASyntaxLink.LastArray)
        {
            PdfASyntaxStreams.QueueType3Glyphs(walk);
        }
    }

    /// <summary>Gets a name operand, decoding it again from the content.</summary>
    /// <param name="reader">The reader; its position is kept.</param>
    /// <param name="walk">The walk.</param>
    /// <param name="fromEnd">The operand's position from the end: 1 for the last.</param>
    /// <returns>The name, or <see langword="null"/> when that operand is not a name.</returns>
    private static PdfName? LastName(ref PdfASyntaxReader reader, PdfASyntaxWalk walk, int fromEnd)
    {
        var operands = walk.Operands;
        if (operands.Count < fromEnd || operands[^fromEnd] is not { Kind: PdfASyntaxValue.Name } operand)
        {
            return null;
        }

        var position = reader.Position;
        reader.Position = operand.Start;
        reader.Next();
        var name = walk.Store.Names.Intern(reader.Value);
        reader.Position = position;
        return name;
    }

    /// <summary>Determines whether a value is a number.</summary>
    /// <param name="kind">The value's kind.</param>
    /// <returns><see langword="true"/> for integers and reals.</returns>
    private static bool IsNumber(PdfASyntaxValue kind) => kind is PdfASyntaxValue.Integer or PdfASyntaxValue.Real;

    /// <summary>Forgets the operands of the last operator.</summary>
    /// <param name="walk">The walk.</param>
    private static void ClearOperands(PdfASyntaxWalk walk)
    {
        walk.Operands.Clear();
        walk.OperandFindings.Discard();
    }
}
