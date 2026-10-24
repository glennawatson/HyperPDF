// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using BenchmarkDotNet.Attributes;
using HyperPdf.Core.Forms.Scripting;

namespace HyperPdf.Benchmarks;

/// <summary>Measures reading and running the sandboxed form scripts; allocations come from the EventPipe trace.</summary>
public class FormScriptProgramBenchmarks
{
    /// <summary>A calculation that multiplies two fields.</summary>
    private const string Calculation = "var q = this.getField('Qty').value; var p = this.getField('Price').value; event.value = q * p;";

    /// <summary>A format script that writes a currency value.</summary>
    private const string Formatting = "event.value = util.printf('$%,0.2f', event.value);";

    /// <summary>A script with a loop that adds up the numbers from one to two hundred.</summary>
    private const string Looping = "var t = 0; for (var i = 1; i <= 200; i++) { t += i; } event.value = t;";

    /// <summary>The calculation, read once.</summary>
    private FormScriptProgram _calculation = null!;

    /// <summary>The format script, read once.</summary>
    private FormScriptProgram _formatting = null!;

    /// <summary>The loop, read once.</summary>
    private FormScriptProgram _looping = null!;

    /// <summary>The form the scripts run in.</summary>
    private BenchmarkHost _host = null!;

    /// <summary>Reads the scripts and makes the form.</summary>
    [GlobalSetup]
    public void Setup()
    {
        _host = new();
        _calculation = Read(Calculation);
        _formatting = Read(Formatting);
        _looping = Read(Looping);
    }

    /// <summary>Reads a calculation script into a program.</summary>
    /// <returns>Whether it could be run.</returns>
    [Benchmark]
    public bool ParseCalculation() => FormScriptProgram.TryParse(Calculation, out _, out _);

    /// <summary>Runs the calculation.</summary>
    /// <returns>The result length.</returns>
    [Benchmark]
    public int RunCalculation() => _calculation.Run(_host, FormScriptEvent.ForValue(string.Empty)).Value.Length;

    /// <summary>Runs the currency format script.</summary>
    /// <returns>The result length.</returns>
    [Benchmark]
    public int RunFormat() => _formatting.Run(_host, FormScriptEvent.ForValue("1234567.891")).Value.Length;

    /// <summary>Runs the loop.</summary>
    /// <returns>The result length.</returns>
    [Benchmark]
    public int RunLoop() => _looping.Run(_host, FormScriptEvent.ForValue(string.Empty)).Value.Length;

    /// <summary>Reads a script.</summary>
    /// <param name="script">The script.</param>
    /// <returns>The program.</returns>
    /// <exception cref="InvalidOperationException">The script is not within what the sandbox reads.</exception>
    private static FormScriptProgram Read(string script) =>
        FormScriptProgram.TryParse(script, out var program, out var reason) ? program! : throw new InvalidOperationException(reason);

    /// <summary>A form of two fields, for the scripts to read.</summary>
    private sealed class BenchmarkHost : IFormScriptHost
    {
        /// <inheritdoc/>
        public string? GetFieldValue(string fieldName) => fieldName == "Qty" ? "4" : "12.5";

        /// <inheritdoc/>
        public void SetFieldValue(string fieldName, string value)
        {
        }

        /// <inheritdoc/>
        public void SetFieldDisplay(string fieldName, FormFieldDisplay display)
        {
        }

        /// <inheritdoc/>
        public void SetFieldReadOnly(string fieldName, bool readOnly)
        {
        }

        /// <inheritdoc/>
        public void Tell(string message)
        {
        }
    }
}
