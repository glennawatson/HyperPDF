// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using HyperPdf.Core.Forms.Scripting;

namespace HyperPdf.Core.Tests.Forms;

/// <summary>Checks the sandboxed interpreter that runs the form scripts that are not one of the built-in functions.</summary>
public sealed class FormScriptProgramTests
{
    /// <summary>The length of a script past the longest the sandbox reads.</summary>
    private const int TooLongScript = 25_000;

    /// <summary>The nesting of brackets past the deepest the sandbox reads.</summary>
    private const int TooDeepNesting = 200;

    /// <summary>The count of statements past the most tokens the sandbox reads.</summary>
    private const int TooManyStatements = 7_000;

    /// <summary>The name of a field the scripts change.</summary>
    private const string SpouseField = "Spouse";

    /// <summary>A value a refused or stopped script leaves alone.</summary>
    private const string KeptValue = "value";

    /// <summary>A calculation multiplies two fields and leaves the product in the event value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task CalculatesFromOtherFields()
    {
        var host = new FakeScriptHost { Values = { ["Qty"] = "4", ["Price"] = "12.5" } };

        var outcome = Run("var q = this.getField(\"Qty\").value; var p = this.getField('Price').value; event.value = q * p;", string.Empty, host);

        await Assert.That(outcome.Completed).IsTrue();
        await Assert.That(outcome.Value).IsEqualTo("50");
    }

    /// <summary>A validate script that sets the return code to false refuses the value and shows its message.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesAValueWithTheScriptsMessage()
    {
        const string Script = "if (event.value < 0) { app.alert(\"Enter zero or more.\"); event.rc = false; }";

        var refused = Run(Script, "-3");
        var accepted = Run(Script, "5");

        await Assert.That(refused.Accepted).IsFalse();
        await Assert.That(refused.Message).IsEqualTo("Enter zero or more.");
        await Assert.That(accepted.Accepted).IsTrue();
        await Assert.That(accepted.Messages).IsEmpty();
    }

    /// <summary>The printf function writes decimals, widths, thousands styles, strings and hexadecimal.</summary>
    /// <param name="format">The format.</param>
    /// <param name="value">The value.</param>
    /// <param name="expected">The text.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("%,0.2f", "1234567.891", "1,234,567.89")]
    [Arguments("%,2.2f", "1234567.891", "1.234.567,89")]
    [Arguments("%,3.1f", "1234.56", "1234,6")]
    [Arguments("%.0f", "2.6", "3")]
    [Arguments("%d items", "7.9", "7 items")]
    [Arguments("[%5d]", "42", "[   42]")]
    [Arguments("[%-5d]", "42", "[42   ]")]
    [Arguments("[%05d]", "42", "[00042]")]
    [Arguments("%+d", "42", "+42")]
    [Arguments("%x", "255", "ff")]
    [Arguments("%X", "255", "FF")]
    [Arguments("100%%", "0", "100%")]
    public async Task PrintsLikeUtilPrintf(string format, string value, string expected)
    {
        var script = $"event.value = util.printf(\"{format}\", {value});";

        await Assert.That(Run(script, string.Empty).Value).IsEqualTo(expected);
    }

    /// <summary>The printf function writes text with %s and cuts it to the precision.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task PrintsStrings() =>
        await Assert.That(Run("event.value = util.printf(\"%s-%.3s\", \"ab\", \"abcdef\");", string.Empty).Value).IsEqualTo("ab-abc");

    /// <summary>Loops, conditions and compound assignment give the usual results.</summary>
    /// <param name="script">The script.</param>
    /// <param name="expected">The event value afterwards.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("var t = 0; for (var i = 1; i <= 10; i++) { t += i; } event.value = t;", "55")]
    [Arguments("var t = 0; var i = 0; while (true) { i++; if (i > 5) { break; } if (i == 2) { continue; } t += i; } event.value = t;", "13")]
    [Arguments("var a = [3, 1, 2]; a.push(9); event.value = a.length + ':' + a[3] + ':' + a.join('-');", "4:9:3-1-2-9")]
    [Arguments("event.value = (1 + 2) * 3 - 4 / 2 % 3;", "7")]
    [Arguments("event.value = 5 > 3 ? 'yes' : 'no';", "yes")]
    [Arguments("event.value = (0 || 'fallback') + (1 && 'both');", "fallbackboth")]
    [Arguments("event.value = '5' + 5;", "55")]
    [Arguments("event.value = '5' * '2';", "10")]
    [Arguments("event.value = 1 == '1' && 1 !== '1' && null == undefined && null !== undefined;", "true")]
    [Arguments("event.value = typeof 'a' + typeof 1 + typeof undefined;", "stringnumberundefined")]
    [Arguments("event.value = Math.round(2.5) + Math.max(1, 7, 3) + Math.min(4, 2) + Math.pow(2, 3) + Math.floor(1.9);", "21")]
    [Arguments("event.value = (1234.5678).toFixed(2);", "1234.57")]
    [Arguments("event.value = 'Hello'.toUpperCase().substring(1, 3) + 'abc'.indexOf('c') + 'abc'.length;", "EL23")]
    [Arguments("event.value = parseInt('42px') + parseFloat('1.5') + Number('2');", "45.5")]
    [Arguments("// a comment\nvar x = 2; /* another */ event.value = x;", "2")]
    public async Task EvaluatesTheLanguage(string script, string expected) =>
        await Assert.That(Run(script, string.Empty).Value).IsEqualTo(expected);

    /// <summary>A script can show or hide a field, make it read-only, and set another field's value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ChangesOtherFields()
    {
        var host = new FakeScriptHost { Values = { [SpouseField] = "x", ["Other"] = string.Empty } };

        var outcome = Run(
            $"var f = this.getField('{SpouseField}'); f.display = display.hidden; f.readonly = true; this.getField('Other').value = 'copied ' + f.value; this.getField('Missing').value = 'x';",
            string.Empty,
            host);

        await Assert.That(outcome.Completed).IsFalse();
        await Assert.That(host.Displays[SpouseField]).IsEqualTo(FormFieldDisplay.Hidden);
        await Assert.That(host.ReadOnly[SpouseField]).IsTrue();
        await Assert.That(host.Values["Other"]).IsEqualTo("copied x");
    }

    /// <summary>The display constants map to where a field shows.</summary>
    /// <param name="name">The constant.</param>
    /// <param name="expected">The setting.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("visible", FormFieldDisplay.Visible)]
    [Arguments("hidden", FormFieldDisplay.Hidden)]
    [Arguments("noPrint", FormFieldDisplay.NoPrint)]
    [Arguments("noView", FormFieldDisplay.NoView)]
    public async Task MapsTheDisplayConstants(string name, FormFieldDisplay expected)
    {
        var host = new FakeScriptHost { Values = { ["F"] = string.Empty } };

        _ = Run($"this.getField('F').display = display.{name};", string.Empty, host);

        await Assert.That(host.Displays["F"]).IsEqualTo(expected);
    }

    /// <summary>A missing field is null, so a script can test for it.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReportsAMissingFieldAsNull() =>
        await Assert.That(Run("event.value = this.getField('Nope') == null ? 'none' : 'found';", string.Empty).Value).IsEqualTo("none");

    /// <summary>Scripts that need more than the sandbox gives are not read or are stopped, and never run the call.</summary>
    /// <param name="script">The script.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("function f() { return 1; } f();")]
    [Arguments("var x = new Date();")]
    [Arguments("eval('1')")]
    [Arguments("import x;")]
    [Arguments("try { x(); } catch (e) {}")]
    [Arguments("var s = `template`;")]
    [Arguments("var o = {a: 1};")]
    [Arguments("a = b @ c;")]
    [Arguments("var = 1;")]
    [Arguments("if (true {")]
    [Arguments("1 = 2;")]
    [Arguments("var s = 'unterminated;")]
    public async Task RefusesWhatTheSandboxDoesNotHave(string script)
    {
        var read = FormScriptProgram.TryParse(script, out var program, out var reason);

        await Assert.That(read).IsFalse();
        await Assert.That(program).IsNull();
        await Assert.That(reason).IsNotEmpty();
    }

    /// <summary>Calls to the app, files, the network and the document that the sandbox lacks are refused before the script runs at all.</summary>
    /// <param name="script">The script.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("app.launchURL('http://example.com');")]
    [Arguments("app.execMenuItem('Save');")]
    [Arguments("this.mailDoc();")]
    [Arguments("this.submitForm('http://example.com');")]
    [Arguments("util.readFileIntoStream('/etc/passwd');")]
    [Arguments("app.setTimeOut('x', 1);")]
    [Arguments("Collab.getURL('x');")]
    [Arguments("console.println('x');")]
    [Arguments("AFMergeChange(event);")]
    [Arguments("Math.random();")]
    [Arguments("display.go();")]
    public async Task RefusesCallsItDoesNotHave(string script)
    {
        var read = FormScriptProgram.TryParse($"event.value = 'changed'; {script}", out var program, out var reason);

        await Assert.That(read && program is not null).IsFalse();
        await Assert.That(reason).IsNotEmpty();
    }

    /// <summary>A call the sandbox lacks on an object it hands out at run time stops the script at that call and leaves the value alone.</summary>
    /// <param name="script">The script.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("this.getField('F').setAction('Calculate', 'x');")]
    [Arguments("var f = this.getField('F'); f.browseForFileToSubmit();")]
    [Arguments("'text'.fontSize();")]
    [Arguments("event.value.foo();")]
    [Arguments("(5).toPrecision(2);")]
    public async Task StopsAtMethodsOfValuesItDoesNotHave(string script)
    {
        var host = new FakeScriptHost { Values = { ["F"] = "a" } };

        var outcome = Run($"event.value = 'changed'; {script}", KeptValue, host);

        await Assert.That(outcome.Completed).IsFalse();
        await Assert.That(outcome.Value).IsEqualTo(KeptValue);
        await Assert.That(outcome.Accepted).IsTrue();
        await Assert.That(outcome.Message).IsNotEmpty();
    }

    /// <summary>A long chain of operators makes a tree that is too deep to walk safely, so it is refused.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesTooDeepChains()
    {
        var chain = string.Join('+', Enumerable.Repeat("1", TooDeepNesting));

        await Assert.That(FormScriptProgram.TryParse($"event.value = {chain};", out _, out _)).IsFalse();
    }

    /// <summary>A script that never ends is stopped, and so is one that builds a huge array.</summary>
    /// <param name="script">The script.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("while (true) { }")]
    [Arguments("for (;;) { var x = 1; }")]
    [Arguments("var a = []; while (true) { a.push(1); }")]
    public async Task StopsRunawayScripts(string script)
    {
        var outcome = Run(script, "kept");

        await Assert.That(outcome.Completed).IsFalse();
        await Assert.That(outcome.Value).IsEqualTo("kept");
    }

    /// <summary>Very long and very deeply nested scripts are refused before they run.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RefusesHugeScripts()
    {
        var tooLong = $"{new string(' ', TooLongScript)}event.value = 1;";
        var tooDeep = $"{new string('(', TooDeepNesting)}1{new string(')', TooDeepNesting)}";
        var tooMany = string.Concat(Enumerable.Repeat("1;", TooManyStatements));

        await Assert.That(FormScriptProgram.TryParse(tooLong, out _, out _)).IsFalse();
        await Assert.That(FormScriptProgram.TryParse(tooDeep, out _, out _)).IsFalse();
        await Assert.That(FormScriptProgram.TryParse(tooMany, out _, out _)).IsFalse();
    }

    /// <summary>Unknown names, calls on non-objects and writes to non-objects stop the script.</summary>
    /// <param name="script">The script.</param>
    /// <returns>A task.</returns>
    [Test]
    [Arguments("event.value = unknownThing;")]
    [Arguments("undefined.x = 1;")]
    [Arguments("(1)();")]
    [Arguments("event.value = 1 ** 2;")]
    public async Task StopsAtUnknownNamesAndBadTargets(string script)
    {
        var read = FormScriptProgram.TryParse(script, out var program, out _);

        await Assert.That(!read || !program!.Run(new FakeScriptHost(), FormScriptEvent.ForValue("kept")).Completed).IsTrue();
    }

    /// <summary>The event carries the typed change and whether the value is being committed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task GivesTheScriptItsEvent()
    {
        var outcome = Run("event.value = event.change + '/' + event.willCommit + '/' + event.value;", new FormScriptEvent("old", "new", true), null);

        await Assert.That(outcome.Value).IsEqualTo("new/true/old");
    }

    /// <summary>The scripts of a field go through <see cref="FormScriptRuns"/> whether they are built-in or sandboxed.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task RunsAFieldsScripts()
    {
        var host = new FakeScriptHost { Values = { ["A"] = "2", ["B"] = "3" } };
        var format = FormScript.Parse("event.value = util.printf('$%,0.2f', event.value);");
        var builtIn = FormScript.Parse("AFNumber_Format(2, 0, 0, 0, \"$\", true);");
        var keystroke = FormScript.Parse("if (event.willCommit && event.value.length > 3) { app.alert('Too long.'); event.rc = false; }");
        var validate = FormScript.Parse("if (event.value != 'ok') { event.rc = false; }");
        var calculate = FormScript.Parse("event.value = this.getField('A').value + this.getField('B').value;");

        await Assert.That(format.Function).IsEqualTo(FormScriptFunction.Program);
        await Assert.That(FormScriptRuns.Format(format, "1234.5", host)).IsEqualTo("$1,234.50");
        await Assert.That(FormScriptRuns.Format(builtIn, "1234.5", host)).IsEqualTo("$1,234.50");
        await Assert.That(FormScriptRuns.Accepts(keystroke, "abcd", host, out var tooLong)).IsFalse();
        await Assert.That(tooLong).IsEqualTo("Too long.");
        await Assert.That(FormScriptRuns.Accepts(keystroke, "abc", host, out _)).IsTrue();
        await Assert.That(FormScriptRuns.Validate(validate, "no", host, out var message)).IsFalse();
        await Assert.That(message).IsNotEmpty();
        await Assert.That(FormScriptRuns.Validate(validate, "ok", host, out _)).IsTrue();
        await Assert.That(FormScriptRuns.TryCalculate(calculate, string.Empty, host, out var total)).IsTrue();
        await Assert.That(total).IsEqualTo("5");
        await Assert.That(FormScriptRuns.TryCalculate(builtIn, string.Empty, host, out _)).IsFalse();
    }

    /// <summary>A script that is stopped never refuses the reader's value.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task NeverBlocksTheReaderWhenAScriptIsStopped()
    {
        var host = new FakeScriptHost();
        var script = FormScript.Parse("app.launchURL('x'); event.rc = false;");

        await Assert.That(FormScriptRuns.Accepts(script, KeptValue, host, out var message)).IsTrue();
        await Assert.That(message).IsEmpty();
        await Assert.That(FormScriptRuns.Format(script, KeptValue, host)).IsEqualTo(KeptValue);
    }

    /// <summary>The longest operator wins, so strict equality and compound assignment are not split.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsTheLongestOperator() =>
        await Assert.That(Run("var a = 1; a += 2; a -= 1; a *= 5; a /= 2; event.value = (a === 5) + ':' + (a !== 5) + ':' + (a <= 5) + ':' + (a >= 6);", string.Empty).Value)
            .IsEqualTo("true:false:true:false");

    /// <summary>Runs a script on a value and gives what it did.</summary>
    /// <param name="script">The script.</param>
    /// <param name="value">The value of <c>event.value</c>.</param>
    /// <returns>The outcome.</returns>
    private static FormScriptOutcome Run(string script, string value) => Run(script, FormScriptEvent.ForValue(value), null);

    /// <summary>Runs a script on a value in a form and gives what it did.</summary>
    /// <param name="script">The script.</param>
    /// <param name="value">The value of <c>event.value</c>.</param>
    /// <param name="host">The form.</param>
    /// <returns>The outcome.</returns>
    private static FormScriptOutcome Run(string script, string value, FakeScriptHost host) => Run(script, FormScriptEvent.ForValue(value), host);

    /// <summary>Runs a script for an event and gives what it did.</summary>
    /// <param name="script">The script.</param>
    /// <param name="input">The event.</param>
    /// <param name="host">The form; a new empty one when <see langword="null"/>.</param>
    /// <returns>The outcome.</returns>
    /// <exception cref="InvalidOperationException">The script is not within what the sandbox reads.</exception>
    private static FormScriptOutcome Run(string script, FormScriptEvent input, FakeScriptHost? host) =>
        FormScriptProgram.TryParse(script, out var program, out var reason)
            ? program!.Run(host ?? new FakeScriptHost(), input)
            : throw new InvalidOperationException(reason);
}
