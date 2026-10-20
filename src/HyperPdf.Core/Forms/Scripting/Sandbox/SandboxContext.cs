// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>The state of one run of a form script: its variables, the objects it can reach, and how much work it has done.</summary>
internal sealed class SandboxContext
{
    /// <summary>The most steps one run may take, so a script cannot run forever.</summary>
    internal const int MaxSteps = 100_000;

    /// <summary>The objects the script can name: <c>this</c>, <c>event</c>, <c>app</c>, <c>util</c>, <c>Math</c>, <c>display</c> and the constant words.</summary>
    private readonly Dictionary<string, SandboxValue> _globals = [with(StringComparer.Ordinal)];

    /// <summary>The variables the script declared.</summary>
    private readonly Dictionary<string, SandboxValue> _variables = [with(StringComparer.Ordinal)];

    /// <summary>The steps taken so far.</summary>
    private int _steps;

    /// <summary>Gets or sets what a <c>break</c> or <c>continue</c> has asked for.</summary>
    internal SandboxSignal Signal { get; set; }

    /// <summary>Makes the context of a run.</summary>
    /// <param name="host">The document.</param>
    /// <param name="input">The event the script runs for.</param>
    /// <param name="messages">Receives the messages the script shows.</param>
    /// <returns>The context.</returns>
    internal static SandboxContext Create(IFormScriptHost host, FormScriptEvent input, List<string> messages)
    {
        var context = new SandboxContext();
        context._globals["true"] = SandboxValue.True;
        context._globals["false"] = SandboxValue.False;
        context._globals["null"] = SandboxValue.Null;
        context._globals["undefined"] = SandboxValue.Undefined;
        context._globals["this"] = SandboxValue.Of(new SandboxDocument(host));
        context._globals["event"] = SandboxValue.Of(Event(input));
        context._globals["app"] = SandboxValue.Of(new SandboxApp(messages));
        context._globals["util"] = SandboxValue.Of(new SandboxUtil());
        context._globals["Math"] = SandboxValue.Of(new SandboxMath());
        context._globals["display"] = SandboxValue.Of(Display());
        return context;
    }

    /// <summary>Reads the <c>event</c> object after the run.</summary>
    /// <param name="name">The property.</param>
    /// <returns>The value.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal SandboxValue ReadEvent(string name) => _globals["event"].AsObject()!.Get(name);

    /// <summary>Counts a step.</summary>
    /// <exception cref="SandboxException">The script has taken too many steps.</exception>
    internal void Step()
    {
        _steps++;
        if (_steps > MaxSteps)
        {
            throw new SandboxException("The script took too long to run.");
        }
    }

    /// <summary>Reads a variable, or a global object when there is no variable of that name.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The value.</returns>
    /// <exception cref="SandboxException">There is nothing of that name.</exception>
    internal SandboxValue Lookup(string name) =>
        _variables.TryGetValue(name, out var variable) || _globals.TryGetValue(name, out variable)
            ? variable
            : throw new SandboxException($"The script uses '{name}', which is not available to form scripts.");

    /// <summary>Sets a variable.</summary>
    /// <param name="name">The name.</param>
    /// <param name="value">The value.</param>
    internal void Declare(string name, SandboxValue value) => _variables[name] = value;

    /// <summary>Makes the <c>event</c> object.</summary>
    /// <param name="input">The event.</param>
    /// <returns>The object.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SandboxBag Event(FormScriptEvent input) =>
        new SandboxBag()
            .With("value", SandboxValue.Of(input.Value))
            .With("change", SandboxValue.Of(input.Change))
            .With("willCommit", SandboxValue.Of(input.WillCommit))
            .With("rc", SandboxValue.True);

    /// <summary>Makes the <c>display</c> object, which names where a field shows.</summary>
    /// <returns>The object.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    private static SandboxBag Display() =>
        new SandboxBag()
            .With("visible", SandboxValue.Of((double)FormFieldDisplay.Visible))
            .With("hidden", SandboxValue.Of((double)FormFieldDisplay.Hidden))
            .With("noPrint", SandboxValue.Of((double)FormFieldDisplay.NoPrint))
            .With("noView", SandboxValue.Of((double)FormFieldDisplay.NoView));
}
