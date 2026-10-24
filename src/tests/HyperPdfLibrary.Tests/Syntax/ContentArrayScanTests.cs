// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Text;
using HyperPdfLibrary.Content;

namespace HyperPdfLibrary.Tests.Syntax;

/// <summary>Tests that array operands are found whole, whatever strings, comments and nesting they hold.</summary>
public sealed class ContentArrayScanTests
{
    /// <summary>Arrays with tricky contents end at their own closing bracket.</summary>
    /// <param name="content">The content with one array operand and the operator after it.</param>
    /// <param name="expectedBody">The bytes expected between the brackets.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("[(a) -20 (b)] TJ ET", "(a) -20 (b)")]
    [Arguments("[(a\\)b) 1 (c(d)e)] TJ ET", "(a\\)b) 1 (c(d)e)")]
    [Arguments("[<41> <4 2>] TJ ET", "<41> <4 2>")]
    [Arguments("[(]) <5D>] TJ ET", "(]) <5D>")]
    [Arguments("[ % ] not the end\n(x)] TJ ET", " % ] not the end\n(x)")]
    [Arguments("[[1 2] 3] TJ ET", "[1 2] 3")]
    [Arguments("[] TJ ET", "")]
    [Arguments("[(\\\\)] TJ ET", "(\\\\)")]
    public async Task Array_FindsItsClosingBracket(string content, string expectedBody)
    {
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var bytes = Encoding.Latin1.GetBytes(content);
        var reader = new ContentReader(bytes, new(), operands);

        var found = reader.Next(out var first);
        var body = Encoding.Latin1.GetString(reader.Body(reader.Operand(0)));
        var more = reader.Next(out var second);

        await Assert.That(found).IsTrue();
        await Assert.That(first).IsEqualTo(ContentOperator.ShowTextArray);
        await Assert.That(body).IsEqualTo(expectedBody);
        await Assert.That(more).IsTrue();
        await Assert.That(second).IsNotEqualTo(ContentOperator.ShowTextArray);
    }

    /// <summary>An array that never closes still reads without failing, as it did before.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task Array_WithoutEnd_DoesNotFail()
    {
        Span<ContentOperand> operands = stackalloc ContentOperand[ContentReader.OperandSlots];
        var reader = new ContentReader("[(a) 1 (b"u8, new(), operands);

        var found = reader.Next(out _);

        await Assert.That(found).IsFalse();
    }
}
