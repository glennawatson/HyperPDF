// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Forms.Scripting;

namespace HyperPdf.Core.Tests.Forms;

/// <summary>Checks the form scripts HyperPDF runs itself: Recognised PDF functions and simplified field notation, and nothing else.</summary>
public sealed class FormScriptTests
{
    /// <summary>The position of the currency in <c>AFNumber_Format</c>.</summary>
    private const int CurrencyArgument = 4;

    /// <summary>The position of the currency-first flag.</summary>
    private const int CurrencyFirstArgument = 5;

    /// <summary>The year of the date parsed.</summary>
    private const int ParsedYear = 2026;

    /// <summary>The month of the date parsed.</summary>
    private const int ParsedMonth = 3;

    /// <summary>The day of the date parsed.</summary>
    private const int ParsedDay = 7;

    /// <summary>The end of a selection inside "12345".</summary>
    private const int SelectionEnd = 3;

    /// <summary>The end of "12345".</summary>
    private const int ValueEnd = 5;

    /// <summary>A position past the end of "123".</summary>
    private const int PastEnd = 9;

    /// <summary>The tolerance of calculated results.</summary>
    private const double Tolerance = 1e-9;

    /// <summary>The values of the fields in the calculations.</summary>
    private static readonly Dictionary<string, string> Values = new(StringComparer.Ordinal) { ["Price"] = "$12.50", ["Quantity"] = "4", ["Unit Cost"] = "3", ["Empty"] = string.Empty };

    /// <summary>Recognised PDF calls are recognised with their arguments; anything else is not run.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RecognisesBuiltInCalls()
    {
        var number = FormScript.Parse("AFNumber_Format(2, 0, 0, 0, \"$\", true);");
        var simple = FormScript.Parse("AFSimple_Calculate(\"SUM\", new Array (\"Price\", \"Quantity\"));");
        var notation = FormScript.Parse("/** BVCALC Price * Quantity EVCALC **/ event.value = AFMakeNumber(getField(\"Price\").value) * 2");
        var hostile = FormScript.Parse("app.launchURL(\"http://example.com\"); this.mailDoc();");

        await Assert.That(number.Function).IsEqualTo(FormScriptFunction.Number);
        await Assert.That(number.Text(CurrencyArgument, string.Empty)).IsEqualTo("$");
        await Assert.That(number.Flag(CurrencyFirstArgument)).IsTrue();
        await Assert.That(simple.Function).IsEqualTo(FormScriptFunction.Simple);
        await Assert.That(simple.Fields).IsEquivalentTo(["Price", "Quantity"]);
        await Assert.That(notation.Function).IsEqualTo(FormScriptFunction.Expression);
        await Assert.That(notation.Expression).IsEqualTo("Price * Quantity");
        await Assert.That(hostile.Function).IsEqualTo(FormScriptFunction.Unknown);
    }

    /// <summary>Numbers, percentages, dates and special formats are written in the expected PDF format.</summary>
    /// <param name="script">The format script.</param>
    /// <param name="value">The value.</param>
    /// <param name="expected">The formatted value.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("AFNumber_Format(2, 0, 0, 0, \"$\", true);", "1234.5", "$1,234.50")]
    [Arguments("AFNumber_Format(2, 2, 0, 0, \" €\", false);", "1234.5", "1.234,50 €")]
    [Arguments("AFNumber_Format(0, 1, 2, 0, \"\", false);", "-42", "(42)")]
    [Arguments("AFPercent_Format(1, 0);", "0.256", "25.6%")]
    [Arguments("AFDate_FormatEx(\"dd/mm/yyyy\");", "2026-03-07", "07/03/2026")]
    [Arguments("AFDate_FormatEx(\"mmm d, yyyy\");", "2026-03-07", "Mar 7, 2026")]
    [Arguments("AFDate_Format(2);", "2026-03-07", "03/07/26")]
    [Arguments("AFDate_Format(6);", "2026-03-07", "07-Mar-26")]
    [Arguments("AFDate_Format(11);", "2026-03-07", "March 7, 2026")]
    [Arguments("AFTime_FormatEx(\"HH:MM:ss\");", "13:45:09", "13:45:09")]
    [Arguments("AFTime_Format(1);", "13:45", "1:45 PM")]
    [Arguments("AFTime_Format(0);", "9:05 AM", "09:05")]
    [Arguments("AFSpecial_Format(2);", "0412345678", "(041) 234-5678")]
    [Arguments("AFSpecial_Format(3);", "123456789", "123-45-6789")]
    public async Task Formats(string script, string value, string expected) =>
        await Assert.That(FormScriptEngine.Format(FormScript.Parse(script), value)).IsEqualTo(expected);

    /// <summary>What is typed is checked: numbers in number fields, real dates in date fields, the right digits in special fields.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChecksWhatIsTyped()
    {
        var number = FormScript.Parse("AFNumber_Keystroke(2, 0, 0, 0, \"\", true);");
        var date = FormScript.Parse("AFDate_KeystrokeEx(\"dd/mm/yyyy\");");
        var zip = FormScript.Parse("AFSpecial_Keystroke(0);");

        await Assert.That(FormScriptEngine.Accepts(number, "$1,234.50")).IsTrue();
        await Assert.That(FormScriptEngine.Accepts(number, "twelve")).IsFalse();
        await Assert.That(FormScriptEngine.Accepts(date, "31/12/2026")).IsTrue();
        await Assert.That(FormScriptEngine.Accepts(date, "32/13/2026")).IsFalse();
        await Assert.That(FormScriptEngine.Accepts(zip, "40000")).IsTrue();
        await Assert.That(FormScriptEngine.Accepts(zip, "4000")).IsFalse();
        await Assert.That(FormScriptEngine.Accepts(FormScript.None, "anything")).IsTrue();
    }

    /// <summary>Time fields accept real times in their format and refuse other text.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChecksTimes()
    {
        var time = FormScript.Parse("AFTime_KeystrokeEx(\"h:MM tt\");");
        var indexed = FormScript.Parse("AFTime_Keystroke(2);");

        await Assert.That(time.Function).IsEqualTo(FormScriptFunction.Time);
        await Assert.That(FormScriptEngine.Accepts(time, "1:45 PM")).IsTrue();
        await Assert.That(FormScriptEngine.Accepts(time, "25:61")).IsFalse();
        await Assert.That(FormScriptEngine.Accepts(indexed, "13:45:09")).IsTrue();
        await Assert.That(FormScriptEngine.Accepts(indexed, "noon")).IsFalse();
    }

    /// <summary>A helper call ahead of the field's real call does not hide it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task SkipsHelperCalls()
    {
        var script = FormScript.Parse("var merged = AFMergeChange(event); AFNumber_Keystroke(2, 0, 0, 0, \"\", true);");

        await Assert.That(script.Function).IsEqualTo(FormScriptFunction.Number);
        await Assert.That(FormScript.Parse("AFMergeChange(event);").Function).IsEqualTo(FormScriptFunction.Unknown);
    }

    /// <summary><c>AFParseDateEx</c> and <c>AFMergeChange</c> are available as engine helpers.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ParsesDatesAndMergesChanges()
    {
        var parsed = FormScriptEngine.TryParseDate("07/03/2026", "dd/mm/yyyy", out var date);

        await Assert.That(parsed).IsTrue();
        DateTimeOffset expected = new(ParsedYear, ParsedMonth, ParsedDay, 0, 0, 0, TimeSpan.Zero);
        await Assert.That(date).IsEqualTo(expected);
        await Assert.That(FormScriptEngine.TryParseDate("32/03/2026", "dd/mm/yyyy", out _)).IsFalse();
        await Assert.That(FormScriptEngine.MergeChange("12345", 1, SelectionEnd, "ab")).IsEqualTo("1ab45");
        await Assert.That(FormScriptEngine.MergeChange("12345", ValueEnd, ValueEnd, "6")).IsEqualTo("123456");
        await Assert.That(FormScriptEngine.MergeChange("123", PastEnd, 0, "x")).IsEqualTo("x");
    }

    /// <summary>A range validation accepts values within its limits and explains a refusal.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ValidatesRanges()
    {
        var range = FormScript.Parse("AFRange_Validate(true, 0, true, 100);");

        var inside = FormScriptEngine.Validate(range, "50", out _);
        var outside = FormScriptEngine.Validate(range, "150", out var message);

        await Assert.That(inside).IsTrue();
        await Assert.That(outside).IsFalse();
        await Assert.That(message).Contains("between 0 and 100");
    }

    /// <summary>Sums, averages, products, minimums, maximums and expressions are calculated, with formatting ignored.</summary>
    /// <param name="script">The calculate script.</param>
    /// <param name="expected">The result.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("AFSimple_Calculate(\"SUM\", new Array (\"Price\", \"Quantity\", \"Empty\"));", 16.5)]
    [Arguments("AFSimple_Calculate(\"AVG\", \"Price, Quantity\");", 8.25)]
    [Arguments("AFSimple_Calculate(\"PRD\", new Array (\"Price\", \"Quantity\"));", 50)]
    [Arguments("AFSimple_Calculate(\"MIN\", new Array (\"Price\", \"Quantity\"));", 4)]
    [Arguments("AFSimple_Calculate(\"MAX\", new Array (\"Price\", \"Quantity\"));", 12.5)]
    [Arguments("/** BVCALC (Price - 2.5) * Quantity / 2 EVCALC **/", 20)]
    [Arguments("/** BVCALC Unit\\ Cost * -Quantity EVCALC **/", -12)]
    public async Task Calculates(string script, double expected)
    {
        var calculated = FormScriptEngine.TryCalculate(FormScript.Parse(script), static name => Values.GetValueOrDefault(name), out var result);

        await Assert.That(calculated).IsTrue();
        await Assert.That(result).IsEqualTo(expected).Within(Tolerance);
    }

    /// <summary>A malformed or dividing-by-zero expression gives no result rather than a wrong one.</summary>
    /// <param name="expression">The expression.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("Price *")]
    [Arguments("(Price")]
    [Arguments("Price / Empty")]
    public async Task RefusesBadExpressions(string expression) =>
        await Assert.That(FormExpression.TryEvaluate(expression, static name => FormNumbers.TryParse(Values.GetValueOrDefault(name), out var v) ? v : 0, out _)).IsFalse();
}
