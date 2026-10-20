// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Forms.Scripting.Sandbox;

namespace HyperPdf.Core.Forms.Scripting;

/// <summary>
/// A form script that HyperPDF runs itself, in a small sandboxed interpreter of the JavaScript forms use: variables,
/// <c>if</c>, loops, numbers, text, arrays, <c>this.getField</c>, <c>event</c>, <c>util.printf</c>, <c>app.alert</c> and <c>Math</c>.
/// It cannot reach files, the network, timers or other programs. Scripts that need more are not run.
/// </summary>
[DebuggerDisplay("FormScriptProgram: {Source}")]
public sealed class FormScriptProgram
{
    /// <summary>The parsed script.</summary>
    private readonly SandboxNode _root;

    /// <summary>The number of variables the script uses.</summary>
    private readonly int _slotCount;

    /// <summary>Initializes a new instance of the <see cref="FormScriptProgram"/> class.</summary>
    /// <param name="source">The script text.</param>
    /// <param name="root">The parsed script.</param>
    /// <param name="slotCount">The number of variables the script uses.</param>
    private FormScriptProgram(string source, SandboxNode root, int slotCount)
    {
        Source = source;
        _root = root;
        _slotCount = slotCount;
    }

    /// <summary>Gets the script text.</summary>
    public string Source { get; }

    /// <summary>Reads a script.</summary>
    /// <param name="javaScript">The script text.</param>
    /// <param name="program">The script, when it is within what the sandbox runs.</param>
    /// <param name="reason">Why the script is not run, in plain words; empty when it is.</param>
    /// <returns><see langword="true"/> when the script can be run.</returns>
    public static bool TryParse(string? javaScript, out FormScriptProgram? program, out string reason)
    {
        program = null;
        reason = string.Empty;
        if (string.IsNullOrWhiteSpace(javaScript))
        {
            reason = "The field has no script.";
            return false;
        }

        try
        {
            var root = SandboxParser.Parse(javaScript, out var slotCount);
            SandboxValidator.Check(root);
            program = new(javaScript, root, slotCount);
            return true;
        }
        catch (SandboxException exception)
        {
            reason = exception.Message;
            return false;
        }
    }

    /// <summary>Runs the script once.</summary>
    /// <param name="host">The document's fields.</param>
    /// <param name="input">What the script sees as <c>event</c>.</param>
    /// <returns>What the script did. A script that goes beyond the sandbox is stopped, and its outcome says why.</returns>
    public FormScriptOutcome Run(IFormScriptHost host, FormScriptEvent input)
    {
        ArgumentNullException.ThrowIfNull(host);
        using var context = new SandboxContext(host, input, _slotCount);
        try
        {
            _ = SandboxInterpreter.Evaluate(_root, context);
        }
        catch (SandboxException exception)
        {
            host.Tell($"A script in this form was skipped. {exception.Message}");
            return new(false, input.Value, true, [exception.Message]);
        }

        foreach (var message in context.Messages)
        {
            host.Tell(message);
        }

        var value = context.EventValue;
        return new(true, value.Kind == SandboxValueKind.Undefined ? input.Value : value.ToText(), context.ReturnCode.IsTruthy, context.Messages);
    }
}
