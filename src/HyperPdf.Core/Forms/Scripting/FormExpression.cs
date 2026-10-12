// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Globalization;
using System.Text;

namespace HyperPdf.Core.Forms.Scripting;

/// <summary>
/// Evaluates simplified field notation, the arithmetic PDF forms use for calculated fields: numbers, field names,
/// <c>+ - * /</c> and parentheses. Nothing else is accepted, so it cannot do anything but arithmetic. A backslash
/// escapes a character in a field name, such as a space.
/// </summary>
public static class FormExpression
{
    /// <summary>Evaluates an expression.</summary>
    /// <param name="expression">The expression, for example <c>Price * Quantity</c>.</param>
    /// <param name="field">Gets a field's value as a number.</param>
    /// <param name="result">The result.</param>
    /// <returns><see langword="true"/> when the expression is well formed and the result is a finite number.</returns>
    public static bool TryEvaluate(string expression, Func<string, double> field, out double result)
    {
        ArgumentNullException.ThrowIfNull(expression);
        ArgumentNullException.ThrowIfNull(field);
        var parser = new Parser(expression, field);
        result = parser.Sum();
        return parser.AtEnd && double.IsFinite(result);
    }

    /// <summary>A recursive descent parser over the expression's text.</summary>
    /// <param name="text">The expression.</param>
    /// <param name="field">Gets a field's value.</param>
    private sealed class Parser(string text, Func<string, double> field)
    {
        /// <summary>The deepest nesting accepted, so a hostile expression cannot exhaust the stack.</summary>
        private const int MaxDepth = 64;

        /// <summary>The read position.</summary>
        private int _position;

        /// <summary>The current nesting.</summary>
        private int _depth;

        /// <summary>Gets a value indicating whether the whole expression was read.</summary>
        public bool AtEnd
        {
            get
            {
                SkipSpace();
                return _position >= text.Length && _depth == 0;
            }
        }

        /// <summary>Reads terms joined by <c>+</c> and <c>-</c>.</summary>
        /// <returns>The value.</returns>
        public double Sum()
        {
            var value = Product();
            while (TryRead('+') || Peek('-'))
            {
                var minus = TryRead('-');
                var term = Product();
                value = minus ? value - term : value + term;
            }

            return value;
        }

        /// <summary>Determines whether a character is an operator or parenthesis, which ends a field name.</summary>
        /// <param name="c">The character.</param>
        /// <returns><see langword="true"/> for an operator.</returns>
        private static bool IsOperator(char c) => c is '+' or '-' or '*' or '/' or '(' or ')';

        /// <summary>Reads factors joined by <c>*</c> and <c>/</c>.</summary>
        /// <returns>The value.</returns>
        private double Product()
        {
            var value = Factor();
            while (Peek('*') || Peek('/'))
            {
                var divide = TryRead('/');
                _ = TryRead('*');
                var factor = Factor();
                value = divide ? value / factor : value * factor;
            }

            return value;
        }

        /// <summary>Reads a number, a field, a negation or a parenthesised sum.</summary>
        /// <returns>The value, or NaN when malformed.</returns>
        private double Factor()
        {
            SkipSpace();
            if (_position >= text.Length || _depth > MaxDepth)
            {
                return double.NaN;
            }

            if (TryRead('-'))
            {
                return -Factor();
            }

            if (TryRead('('))
            {
                _depth++;
                var inner = Sum();
                _depth--;
                return TryRead(')') ? inner : double.NaN;
            }

            return char.IsAsciiDigit(text[_position]) || text[_position] == '.' ? Number() : Field();
        }

        /// <summary>Reads a number.</summary>
        /// <returns>The number.</returns>
        private double Number()
        {
            var start = _position;
            while (_position < text.Length && (char.IsAsciiDigit(text[_position]) || text[_position] == '.'))
            {
                _position++;
            }

            return double.TryParse(text.AsSpan(start, _position - start), NumberStyles.Float, CultureInfo.InvariantCulture, out var number) ? number : double.NaN;
        }

        /// <summary>Reads a field name and looks its value up.</summary>
        /// <returns>The field's value, or NaN when there is no name.</returns>
        private double Field()
        {
            // Most names have no escapes and are read straight from the text.
            var start = _position;
            while (_position < text.Length && !IsOperator(text[_position]) && !char.IsWhiteSpace(text[_position]) && text[_position] != '\\')
            {
                _position++;
            }

            if (_position >= text.Length || text[_position] != '\\')
            {
                return _position == start ? double.NaN : field(text[start.._position]);
            }

            return field(EscapedName(start));
        }

        /// <summary>Reads the rest of a field name that holds escapes, dropping each backslash and keeping what it escapes.</summary>
        /// <param name="start">Where the name started.</param>
        /// <returns>The name.</returns>
        private string EscapedName(int start)
        {
            var name = new StringBuilder().Append(text, start, _position - start);
            while (_position < text.Length && !IsOperator(text[_position]) && !char.IsWhiteSpace(text[_position]))
            {
                if (text[_position] == '\\' && _position + 1 < text.Length)
                {
                    _position++;
                }

                _ = name.Append(text[_position]);
                _position++;
            }

            return name.ToString();
        }

        /// <summary>Reads a character when it is next.</summary>
        /// <param name="c">The character.</param>
        /// <returns><see langword="true"/> when read.</returns>
        private bool TryRead(char c)
        {
            if (!Peek(c))
            {
                return false;
            }

            _position++;
            return true;
        }

        /// <summary>Determines whether a character is next, after any spaces.</summary>
        /// <param name="c">The character.</param>
        /// <returns><see langword="true"/> when it is.</returns>
        private bool Peek(char c)
        {
            SkipSpace();
            return _position < text.Length && text[_position] == c;
        }

        /// <summary>Skips spaces.</summary>
        private void SkipSpace()
        {
            while (_position < text.Length && char.IsWhiteSpace(text[_position]))
            {
                _position++;
            }
        }
    }
}
