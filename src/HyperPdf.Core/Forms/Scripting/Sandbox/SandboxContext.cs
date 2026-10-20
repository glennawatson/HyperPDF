// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Buffers;
using System.Runtime.CompilerServices;

namespace HyperPdf.Core.Forms.Scripting.Sandbox;

/// <summary>
/// The state of one run of a form script: its variables, the objects it can reach, and how much work it has done. It is also
/// the script's <c>event</c> object, so a run makes one object for both. Parts are made only when the script uses them.
/// </summary>
internal sealed class SandboxContext : SandboxObject, IDisposable
{
    /// <summary>The most steps one run may take, so a script cannot run forever.</summary>
    internal const int MaxSteps = 100_000;

    /// <summary>The <c>util</c> object, which holds no state and is shared.</summary>
    private static readonly SandboxValue UtilValue = SandboxValue.Of(new SandboxUtil());

    /// <summary>The <c>Math</c> object, which holds no state and is shared.</summary>
    private static readonly SandboxValue MathValue = SandboxValue.Of(new SandboxMath());

    /// <summary>The <c>display</c> object, whose constants cannot be changed and is shared.</summary>
    private static readonly SandboxValue DisplayValue = SandboxValue.Of(new SandboxDisplay());

    /// <summary>What a run with no messages gives.</summary>
    private static readonly string[] NoMessages = [];

    /// <summary>The document the script runs in.</summary>
    private readonly IFormScriptHost _host;

    /// <summary>Whether the value is being committed, for <c>event.willCommit</c>.</summary>
    private readonly bool _willCommit;

    /// <summary>The variables of the script by place, rented for the run; null when the script has none.</summary>
    private SandboxValue[]? _slots;

    /// <summary>The typed change the script sees in <c>event.change</c>.</summary>
    private SandboxValue _change;

    /// <summary>The messages the script showed, made when the first is.</summary>
    private List<string>? _messages;

    /// <summary>The <c>this</c> object, made when the script uses it.</summary>
    private SandboxValue _document;

    /// <summary>The <c>app</c> object, made when the script uses it.</summary>
    private SandboxValue _app;

    /// <summary>The steps taken so far.</summary>
    private int _steps;

    /// <summary>Initializes a new instance of the <see cref="SandboxContext"/> class.</summary>
    /// <param name="host">The document.</param>
    /// <param name="input">The event the script runs for.</param>
    /// <param name="slotCount">The number of variables the script uses.</param>
    internal SandboxContext(IFormScriptHost host, FormScriptEvent input, int slotCount)
    {
        _host = host;
        EventValue = SandboxValue.Of(input.Value);
        ReturnCode = SandboxValue.True;
        _change = SandboxValue.Of(input.Change);
        _willCommit = input.WillCommit;
        _slots = slotCount > 0 ? ArrayPool<SandboxValue>.Shared.Rent(slotCount) : null;
    }

    /// <summary>Gets or sets what a <c>break</c> or <c>continue</c> has asked for.</summary>
    internal SandboxSignal Signal { get; set; }

    /// <summary>Gets the value the script leaves in <c>event.value</c>.</summary>
    internal SandboxValue EventValue { get; private set; }

    /// <summary>Gets the return code the script leaves in <c>event.rc</c>; true unless the script refuses the value.</summary>
    internal SandboxValue ReturnCode { get; private set; }

    /// <summary>Gets the messages the script showed.</summary>
    internal IReadOnlyList<string> Messages => _messages is null ? NoMessages : _messages;

    /// <inheritdoc/>
    public void Dispose()
    {
        if (_slots is null)
        {
            return;
        }

        // The array is cleared, so it keeps no objects alive in the pool.
        ArrayPool<SandboxValue>.Shared.Return(_slots, true);
        _slots = null;
    }

    /// <summary>Notes a message the script wants to show.</summary>
    /// <param name="message">The message.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void AddMessage(string message) => (_messages ??= []).Add(message);

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

    /// <summary>Reads a variable.</summary>
    /// <param name="slot">The variable's place.</param>
    /// <returns>The value; undefined until the variable is given one.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal SandboxValue Variable(int slot) => _slots![slot];

    /// <summary>Sets a variable.</summary>
    /// <param name="slot">The variable's place.</param>
    /// <param name="value">The value.</param>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    internal void SetVariable(int slot, SandboxValue value) => _slots![slot] = value;

    /// <summary>Gets a global object or constant word.</summary>
    /// <param name="name">The name.</param>
    /// <returns>The value.</returns>
    /// <exception cref="SandboxException">There is nothing of that name.</exception>
    internal SandboxValue Global(string name) => name switch
    {
        "true" => SandboxValue.True,
        "false" => SandboxValue.False,
        "null" => SandboxValue.Null,
        "undefined" => SandboxValue.Undefined,
        "event" => SandboxValue.Of(this),
        "this" => Document(),
        "app" => App(),
        "util" => UtilValue,
        "Math" => MathValue,
        "display" => DisplayValue,
        _ => throw new SandboxException($"The script uses '{name}', which is not available to form scripts."),
    };

    /// <inheritdoc/>
    internal override SandboxValue Get(string name) => name switch
    {
        "value" => EventValue,
        "rc" => ReturnCode,
        "change" => _change,
        "willCommit" => SandboxValue.Of(_willCommit),
        _ => SandboxValue.Undefined,
    };

    /// <inheritdoc/>
    internal override void Set(string name, SandboxValue value)
    {
        switch (name)
        {
            case "value":
            {
                EventValue = value;
                break;
            }

            case "rc":
            {
                ReturnCode = value;
                break;
            }

            case "change":
            {
                _change = value;
                break;
            }
        }
    }

    /// <summary>Gets the <c>this</c> object, making it on first use.</summary>
    /// <returns>The object.</returns>
    private SandboxValue Document()
    {
        _document = _document.Kind == SandboxValueKind.Object ? _document : SandboxValue.Of(new SandboxDocument(_host));
        return _document;
    }

    /// <summary>Gets the <c>app</c> object, making it on first use.</summary>
    /// <returns>The object.</returns>
    private SandboxValue App()
    {
        _app = _app.Kind == SandboxValueKind.Object ? _app : SandboxValue.Of(new SandboxApp(this));
        return _app;
    }
}
