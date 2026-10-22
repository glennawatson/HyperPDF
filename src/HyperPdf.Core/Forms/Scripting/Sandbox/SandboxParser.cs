// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>
/// Turns the tokens of a form script into a tree. It reads a small part of JavaScript: variables, <c>if</c>, <c>for</c>,
/// <c>while</c>, the usual operators, arrays, member access and calls. Everything else is refused.
/// </summary>
internal sealed class SandboxParser
{
    /// <summary>The deepest nesting of statements and expressions allowed.</summary>
    private const int MaxDepth = 64;

    /// <summary>The number of binary operator levels, from the loosest, that short-circuit.</summary>
    private const int ShortCircuitLevels = 2;

    /// <summary>The binary operators, loosest first. The first two levels short-circuit.</summary>
    private static readonly string[][] Levels =
    [
        ["||"], ["&&"], ["==", "!=", "===", "!=="], ["<", ">", "<=", ">="], ["+", "-"], ["*", "/", "%"],
    ];

    /// <summary>The operators that store a value.</summary>
    private static readonly string[] AssignOperators = ["=", "+=", "-=", "*=", "/="];

    /// <summary>The words JavaScript reserves for features the sandbox does not have.</summary>
    private static readonly string[] Refused =
    [
        "function", "return", "do", "switch", "try", "throw", "new", "delete", "class", "import", "with", "eval", "async", "await", "yield",
    ];

    /// <summary>The tokens.</summary>
    private readonly List<SandboxToken> _tokens;

    /// <summary>The place of each variable in the array a run keeps its variables in, so a run looks variables up by position and not by name.</summary>
    private readonly Dictionary<string, int> _slots = [with(StringComparer.Ordinal)];

    /// <summary>The position of the next token.</summary>
    private int _at;

    /// <summary>The current nesting.</summary>
    private int _depth;

    /// <summary>Initializes a new instance of the <see cref="SandboxParser"/> class.</summary>
    /// <param name="tokens">The tokens, ending with an end token.</param>
    private SandboxParser(List<SandboxToken> tokens) => _tokens = tokens;

    /// <summary>Gets the next token without taking it.</summary>
    private SandboxToken Peek => _tokens[_at];

    /// <summary>Parses a script.</summary>
    /// <param name="source">The script text.</param>
    /// <param name="slotCount">The number of variables the script uses.</param>
    /// <returns>The script as one block.</returns>
    /// <exception cref="SandboxException">The script is not well formed, or uses something the sandbox does not allow.</exception>
    internal static SandboxNode Parse(string source, out int slotCount)
    {
        var parser = new SandboxParser(SandboxLexer.Tokenize(source));
        var statements = new List<SandboxNode>();
        while (parser.Peek.Kind != SandboxTokenKind.End)
        {
            statements.Add(parser.Statement());
        }

        slotCount = parser._slots.Count;
        return SandboxNode.Of(SandboxNodeKind.Block, [.. statements]);
    }

    /// <summary>Determines whether a word is one the sandbox refuses.</summary>
    /// <param name="word">The word.</param>
    /// <returns><see langword="true"/> when it is refused.</returns>
    private static bool IsRefused(string word) => Array.IndexOf(Refused, word) >= 0;

    /// <summary>Makes an increase or decrease node.</summary>
    /// <param name="op">The operator.</param>
    /// <param name="prefix">1 when the operator comes before its target.</param>
    /// <param name="target">The target.</param>
    /// <returns>The node.</returns>
    /// <exception cref="SandboxException">The target cannot be changed.</exception>
    private SandboxNode Update(string op, double prefix, SandboxNode target)
    {
        if (target.Kind is not (SandboxNodeKind.Name or SandboxNodeKind.Member or SandboxNodeKind.Index))
        {
            throw new SandboxException("The script is not well formed: only a variable, property or element can be changed with ++ or --.");
        }

        return new(SandboxNodeKind.Update, op, prefix, [Bind(target)]);
    }

    /// <summary>Makes the node for a name: a variable or global object, which includes the words <c>true</c>, <c>false</c>, <c>null</c> and <c>undefined</c>.</summary>
    /// <param name="word">The name.</param>
    /// <returns>The node; its number is the variable's place, or -1 when the name is not a variable declared before.</returns>
    /// <exception cref="SandboxException">The word is one the sandbox refuses.</exception>
    private SandboxNode Word(string word) =>
        IsRefused(word)
            ? throw new SandboxException($"The script uses '{word}', which form scripts cannot use.")
            : new(SandboxNodeKind.Name, word, _slots.GetValueOrDefault(word, -1), []);

    /// <summary>Gives a name that is assigned to a place for its variable, so that a run finds it by position.</summary>
    /// <param name="target">The target of an assignment or update.</param>
    /// <returns>The target; a name that was not a variable becomes one.</returns>
    private SandboxNode Bind(SandboxNode target) =>
        target is { Kind: SandboxNodeKind.Name, Number: < 0 } ? new(SandboxNodeKind.Name, target.Text, Slot(target.Text), []) : target;

    /// <summary>Gets the place of a variable, giving it one the first time.</summary>
    /// <param name="name">The variable's name.</param>
    /// <returns>The place.</returns>
    private int Slot(string name)
    {
        ref var slot = ref CollectionsMarshal.GetValueRefOrAddDefault(_slots, name, out var exists);
        if (!exists)
        {
            slot = _slots.Count - 1;
        }

        return slot;
    }

    /// <summary>Determines whether the next token is a symbol.</summary>
    /// <param name="symbol">The symbol.</param>
    /// <returns><see langword="true"/> when it is next.</returns>
    private bool IsSymbol(string symbol) => Peek is { Kind: SandboxTokenKind.Symbol } token && token.Text == symbol;

    /// <summary>Determines whether the next token is a symbol of a set.</summary>
    /// <param name="symbols">The symbols.</param>
    /// <returns><see langword="true"/> when one of them is next.</returns>
    private bool IsSymbolOf(string[] symbols) => Peek is { Kind: SandboxTokenKind.Symbol } token && Array.IndexOf(symbols, token.Text) >= 0;

    /// <summary>Takes the next token.</summary>
    /// <returns>The token.</returns>
    private SandboxToken Take()
    {
        var token = _tokens[_at];
        _at += token.Kind == SandboxTokenKind.End ? 0 : 1;
        return token;
    }

    /// <summary>Takes a symbol, or refuses the script.</summary>
    /// <param name="symbol">The symbol that must come next.</param>
    /// <exception cref="SandboxException">Something else comes next.</exception>
    private void Expect(string symbol)
    {
        if (!IsSymbol(symbol))
        {
            throw new SandboxException($"The script is not well formed: expected '{symbol}'.");
        }

        _at++;
    }

    /// <summary>Takes a symbol when it comes next.</summary>
    /// <param name="symbol">The symbol.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private void Accept(string symbol) => _at += IsSymbol(symbol) ? 1 : 0;

    /// <summary>Counts one more level of nesting, refusing a script that nests too deeply.</summary>
    /// <exception cref="SandboxException">The script nests too deeply.</exception>
    private void Enter()
    {
        _depth++;
        if (_depth > MaxDepth)
        {
            throw new SandboxException("The script nests too deeply to run.");
        }
    }

    /// <summary>Reads a statement.</summary>
    /// <returns>The node.</returns>
    private SandboxNode Statement()
    {
        Enter();
        var node = StatementBody();
        _depth--;
        return node;
    }

    /// <summary>Reads a statement at the current nesting.</summary>
    /// <returns>The node.</returns>
    /// <exception cref="SandboxException">The statement is not well formed or uses a word the sandbox refuses.</exception>
    private SandboxNode StatementBody()
    {
        if (IsSymbol(";"))
        {
            _at++;
            return SandboxNode.Of(SandboxNodeKind.Block);
        }

        if (IsSymbol("{"))
        {
            return Block();
        }

        if (Peek.Kind == SandboxTokenKind.Name && Keyword(Peek.Text) is { } keyword)
        {
            return keyword;
        }

        if (Peek.Kind == SandboxTokenKind.Name && IsRefused(Peek.Text))
        {
            throw new SandboxException($"The script uses '{Peek.Text}', which form scripts cannot use.");
        }

        var expression = Expression();
        Accept(";");
        return expression;
    }

    /// <summary>Reads a statement that starts with a keyword.</summary>
    /// <param name="word">The word the statement starts with.</param>
    /// <returns>The node; <see langword="null"/> when the word is not a statement keyword, in which case nothing is taken.</returns>
    private SandboxNode? Keyword(string word) => word switch
    {
        "var" or "let" or "const" => Skip().Declaration(),
        "if" => Skip().If(),
        "for" => Skip().For(),
        "while" => Skip().While(),
        "break" => Skip().Jump(SandboxNodeKind.Break),
        "continue" => Skip().Jump(SandboxNodeKind.Continue),
        _ => null,
    };

    /// <summary>Moves past the next token.</summary>
    /// <returns>The parser, so that the statement can be read on.</returns>
    private SandboxParser Skip()
    {
        _at++;
        return this;
    }

    /// <summary>Reads a block in braces.</summary>
    /// <returns>The node.</returns>
    /// <exception cref="SandboxException">The block is not closed.</exception>
    private SandboxNode Block()
    {
        Expect("{");
        var statements = new List<SandboxNode>();
        while (!IsSymbol("}"))
        {
            if (Peek.Kind == SandboxTokenKind.End)
            {
                throw new SandboxException("The script is not well formed: a block is not closed.");
            }

            statements.Add(Statement());
        }

        _at++;
        return SandboxNode.Of(SandboxNodeKind.Block, [.. statements]);
    }

    /// <summary>Reads the rest of <c>var a = 1, b = 2;</c>.</summary>
    /// <returns>The node.</returns>
    /// <exception cref="SandboxException">A variable has no name.</exception>
    private SandboxNode Declaration()
    {
        var declarations = new List<SandboxNode>();
        do
        {
            var name = Take();
            if (name.Kind != SandboxTokenKind.Name)
            {
                throw new SandboxException("The script is not well formed: a variable needs a name.");
            }

            var slot = Slot(name.Text);
            var initial = IsSymbol("=") ? Initial() : Word("undefined");
            declarations.Add(new(SandboxNodeKind.Declare, name.Text, slot, [initial]));
        }
        while (AcceptComma());

        Accept(";");
        return declarations.Count == 1 ? declarations[0] : SandboxNode.Of(SandboxNodeKind.Block, [.. declarations]);
    }

    /// <summary>Reads <c>= value</c>.</summary>
    /// <returns>The value.</returns>
    private SandboxNode Initial()
    {
        _at++;
        return Assignment();
    }

    /// <summary>Takes a comma when it comes next.</summary>
    /// <returns><see langword="true"/> when there was one.</returns>
    private bool AcceptComma()
    {
        var comma = IsSymbol(",");
        _at += comma ? 1 : 0;
        return comma;
    }

    /// <summary>Reads the rest of an <c>if</c>.</summary>
    /// <returns>The node.</returns>
    private SandboxNode If()
    {
        Expect("(");
        var test = Expression();
        Expect(")");
        var then = Statement();
        if (Peek is not { Kind: SandboxTokenKind.Name, Text: "else" })
        {
            return SandboxNode.Of(SandboxNodeKind.If, test, then);
        }

        _at++;
        return SandboxNode.Of(SandboxNodeKind.If, test, then, Statement());
    }

    /// <summary>Reads the rest of a <c>while</c>.</summary>
    /// <returns>The node.</returns>
    private SandboxNode While()
    {
        Expect("(");
        var test = Expression();
        Expect(")");
        return SandboxNode.Of(SandboxNodeKind.While, test, Statement());
    }

    /// <summary>Reads the rest of a <c>for</c>. Empty parts become empty blocks, and an empty test is always true.</summary>
    /// <returns>The node.</returns>
    private SandboxNode For()
    {
        Expect("(");
        var start = IsSymbol(";") ? Statement() : StatementBody();
        var test = IsSymbol(";") ? Word("true") : Expression();
        Expect(";");
        var step = IsSymbol(")") ? SandboxNode.Of(SandboxNodeKind.Block) : Expression();
        Expect(")");
        return SandboxNode.Of(SandboxNodeKind.For, start, test, step, Statement());
    }

    /// <summary>Reads the rest of a <c>break</c> or <c>continue</c>.</summary>
    /// <param name="kind">Which one.</param>
    /// <returns>The node.</returns>
    private SandboxNode Jump(SandboxNodeKind kind)
    {
        Accept(";");
        return SandboxNode.Of(kind);
    }

    /// <summary>Reads an expression.</summary>
    /// <returns>The node.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private SandboxNode Expression() => Assignment();

    /// <summary>Reads an assignment, or the expression below it.</summary>
    /// <returns>The node.</returns>
    /// <exception cref="SandboxException">The left side cannot be assigned to.</exception>
    private SandboxNode Assignment()
    {
        Enter();
        var left = Conditional();
        if (IsSymbolOf(AssignOperators))
        {
            if (left.Kind is not (SandboxNodeKind.Name or SandboxNodeKind.Member or SandboxNodeKind.Index))
            {
                throw new SandboxException("The script is not well formed: only a variable, property or element can be assigned to.");
            }

            var op = Take().Text;
            var target = Bind(left);
            left = SandboxNode.Of(SandboxNodeKind.Assign, op, target, Assignment());
        }

        _depth--;
        return left;
    }

    /// <summary>Reads a <c>? :</c> choice, or the expression below it.</summary>
    /// <returns>The node.</returns>
    private SandboxNode Conditional()
    {
        var test = Binary(0);
        if (!IsSymbol("?"))
        {
            return test;
        }

        _at++;
        var then = Assignment();
        Expect(":");
        return SandboxNode.Of(SandboxNodeKind.Conditional, test, then, Assignment());
    }

    /// <summary>Reads a binary operator level, or the levels below it.</summary>
    /// <param name="level">The index in <see cref="Levels"/>.</param>
    /// <returns>The node.</returns>
    private SandboxNode Binary(int level)
    {
        if (level >= Levels.Length)
        {
            return Unary();
        }

        var left = Binary(level + 1);
        while (IsSymbolOf(Levels[level]))
        {
            var op = Take().Text;
            var kind = level < ShortCircuitLevels ? SandboxNodeKind.Logical : SandboxNodeKind.Binary;
            left = SandboxNode.Of(kind, op, left, Binary(level + 1));
        }

        return left;
    }

    /// <summary>Reads a prefix operator and its operand, or the expression below.</summary>
    /// <returns>The node.</returns>
    private SandboxNode Unary()
    {
        Enter();
        var node = UnaryBody();
        _depth--;
        return node;
    }

    /// <summary>Reads a prefix operator at the current nesting.</summary>
    /// <returns>The node.</returns>
    private SandboxNode UnaryBody()
    {
        if (IsSymbol("++") || IsSymbol("--"))
        {
            var op = Take().Text;
            return Update(op, 1, Unary());
        }

        if (IsSymbol("!") || IsSymbol("-") || IsSymbol("+") || Peek is { Kind: SandboxTokenKind.Name, Text: "typeof" })
        {
            var op = Take().Text;
            return SandboxNode.Of(SandboxNodeKind.Unary, op, Unary());
        }

        var operand = CallOrMember();
        return IsSymbol("++") || IsSymbol("--") ? Update(Take().Text, 0, operand) : operand;
    }

    /// <summary>Reads a primary expression and the member accesses, element reads and calls after it.</summary>
    /// <returns>The node.</returns>
    /// <exception cref="SandboxException">A property has no name.</exception>
    private SandboxNode CallOrMember()
    {
        var node = Primary();
        var chained = true;
        while (chained)
        {
            if (IsSymbol("."))
            {
                _at++;
                var member = Take();
                node = member.Kind == SandboxTokenKind.Name
                    ? SandboxNode.Of(SandboxNodeKind.Member, member.Text, node)
                    : throw new SandboxException("The script is not well formed: a property needs a name.");
            }
            else if (IsSymbol("["))
            {
                _at++;
                var index = Expression();
                Expect("]");
                node = SandboxNode.Of(SandboxNodeKind.Index, node, index);
            }
            else if (IsSymbol("("))
            {
                _at++;
                node = SandboxNode.Of(SandboxNodeKind.Call, [node, .. Arguments(")")]);
            }
            else
            {
                chained = false;
            }
        }

        return node;
    }

    /// <summary>Reads comma separated expressions up to a closing symbol.</summary>
    /// <param name="close">The closing symbol, which is taken.</param>
    /// <returns>The expressions.</returns>
    /// <exception cref="SandboxException">The list is not well formed.</exception>
    private List<SandboxNode> Arguments(string close)
    {
        var items = new List<SandboxNode>();
        while (!IsSymbol(close))
        {
            items.Add(Expression());
            if (!AcceptComma() && !IsSymbol(close))
            {
                throw new SandboxException($"The script is not well formed: expected ',' or '{close}'.");
            }
        }

        _at++;
        return items;
    }

    /// <summary>Reads a literal, a name, a bracketed expression or an array.</summary>
    /// <returns>The node.</returns>
    /// <exception cref="SandboxException">An expression is missing.</exception>
    private SandboxNode Primary()
    {
        var token = Take();
        return token.Kind switch
        {
            SandboxTokenKind.Number => new(SandboxNodeKind.Number, string.Empty, token.Number, []),
            SandboxTokenKind.String => SandboxNode.Of(SandboxNodeKind.String, token.Text),
            SandboxTokenKind.Name => Word(token.Text),
            SandboxTokenKind.Symbol when token.Text == "(" => Bracketed(),
            SandboxTokenKind.Symbol when token.Text == "[" => SandboxNode.Of(SandboxNodeKind.Array, [.. Arguments("]")]),
            _ => throw new SandboxException("The script is not well formed: an expression is missing."),
        };
    }

    /// <summary>Reads the rest of a bracketed expression.</summary>
    /// <returns>The expression.</returns>
    private SandboxNode Bracketed()
    {
        var inner = Expression();
        Expect(")");
        return inner;
    }
}
