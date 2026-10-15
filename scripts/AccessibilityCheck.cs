#!/usr/bin/env -S dotnet run --file
#:property TargetFramework=net11.0
#:property IsAotCompatible=false
#:property PublishAot=false

// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

// Helpers for dotnet run --file scripts/CheckScreenReader.cs --, which runs them:
//   dotnet run --file scripts/AccessibilityCheck.cs -- create-check-pdf <output PDF>
//   dotnet run --file scripts/AccessibilityCheck.cs -- atspi-walk <bus address> <application> <minimum named controls>
using System.Collections.Frozen;
using System.Diagnostics;
using System.Globalization;
using System.Text;
using System.Text.Json;

return await HyperPdf.Scripts.AccessibilityCheck.RunAsync(args).ConfigureAwait(false);

namespace HyperPdf.Scripts
{
    /// <summary>Writes a small PDF to open, and walks the app's AT-SPI tree checking every control a person uses has a spoken name.</summary>
    internal static class AccessibilityCheck
    {
        /// <summary>The accessible object interface.</summary>
        private const string Accessible = "org.a11y.atspi.Accessible";

        /// <summary>The exit code for success.</summary>
        private const int Success = 0;

        /// <summary>The exit code for a failed check or bad arguments.</summary>
        private const int Failure = 1;

        /// <summary>The arguments atspi-walk takes after its name.</summary>
        private const int WalkArguments = 3;

        /// <summary>The roles that need spoken names.</summary>
        private static readonly FrozenSet<string> Controls = FrozenSet.ToFrozenSet(
        [
            "push button", "toggle button", "combo box", "entry", "text", "check box",
            "radio button", "slider", "page tab", "menu item",
        ], StringComparer.Ordinal);

        /// <summary>Runs a command.</summary>
        /// <param name="args">The command and its arguments.</param>
        /// <returns>The exit code.</returns>
        internal static async Task<int> RunAsync(string[] args) => args switch
        {
            ["create-check-pdf", var output] => await CreateCheckPdfAsync(output).ConfigureAwait(false),
            ["atspi-walk", var address, var application, var minimum] when args.Length == WalkArguments + 1 => await WalkAsync(address, application, minimum).ConfigureAwait(false),
            _ => await UsageAsync().ConfigureAwait(false),
        };

        /// <summary>Prints how to run the commands.</summary>
        /// <returns>The failure exit code.</returns>
        private static async Task<int> UsageAsync()
        {
            await Console.Error.WriteLineAsync("usage: AccessibilityCheck.cs create-check-pdf <output PDF> | atspi-walk <address> <application> <minimum-controls>").ConfigureAwait(false);
            return Failure;
        }

        /// <summary>Writes a one page PDF for the app to open.</summary>
        /// <param name="path">The output file.</param>
        /// <returns>The exit code.</returns>
        private static async Task<int> CreateCheckPdfAsync(string path)
        {
            const string text = "BT /F1 18 Tf 72 700 Td (Screen reader check) Tj ET";
            string[] objects =
            [
                "<< /Type /Catalog /Pages 2 0 R >>",
                "<< /Type /Pages /Kids [3 0 R] /Count 1 >>",
                "<< /Type /Page /Parent 2 0 R /MediaBox [0 0 612 792] /Contents 4 0 R /Resources << /Font << /F1 5 0 R >> >> >>",
                $"<< /Length {text.Length} >>\nstream\n{text}\nendstream",
                "<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>",
            ];
            var output = new StringBuilder("%PDF-1.4\n");
            var offsets = new int[objects.Length];
            for (var index = 0; index < objects.Length; index++)
            {
                offsets[index] = output.Length;
                _ = output.Append(CultureInfo.InvariantCulture, $"{index + 1} 0 obj\n{objects[index]}\nendobj\n");
            }

            var xref = output.Length;
            _ = output.Append(CultureInfo.InvariantCulture, $"xref\n0 {objects.Length + 1}\n0000000000 65535 f \n");
            foreach (var offset in offsets)
            {
                _ = output.Append(CultureInfo.InvariantCulture, $"{offset:D10} 00000 n \n");
            }

            _ = output.Append(CultureInfo.InvariantCulture, $"trailer\n<< /Size {objects.Length + 1} /Root 1 0 R >>\nstartxref\n{xref}\n%%EOF\n");
            await File.WriteAllBytesAsync(path, Encoding.ASCII.GetBytes(output.ToString())).ConfigureAwait(false);
            return Success;
        }

        /// <summary>Walks the app's accessibility tree and reports windows, named controls and controls without a name.</summary>
        /// <param name="address">The accessibility bus address.</param>
        /// <param name="application">The application's bus name.</param>
        /// <param name="minimumText">The fewest named controls expected.</param>
        /// <returns>The exit code.</returns>
        private static async Task<int> WalkAsync(string address, string application, string minimumText)
        {
            if (!int.TryParse(minimumText, NumberStyles.None, CultureInfo.InvariantCulture, out var minimum))
            {
                return await UsageAsync().ConfigureAwait(false);
            }

            var state = await VisitAsync(address, application).ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"windows: [{string.Join(", ", state.Frames)}]").ConfigureAwait(false);
            await Console.Out.WriteLineAsync($"{state.Named} named controls").ConfigureAwait(false);
            foreach (var line in state.Bad)
            {
                await Console.Out.WriteLineAsync($"needs a name -> {line}").ConfigureAwait(false);
            }

            return state.Frames.Count == 0 || state.Named < minimum || state.Bad.Count != 0 ? Failure : Success;
        }

        /// <summary>Visits every accessible object once.</summary>
        /// <param name="address">The accessibility bus address.</param>
        /// <param name="application">The application bus name.</param>
        /// <returns>The inspection results.</returns>
        /// <exception cref="InvalidDataException">A child has no object path.</exception>
        private static async Task<WalkState> VisitAsync(string address, string application)
        {
            var state = new WalkState();
            HashSet<string> seen = [with(comparer: StringComparer.Ordinal)];
            Stack<string> pending = new();
            pending.Push("/org/a11y/atspi/accessible/root");
            while (pending.TryPop(out var path))
            {
                if (!seen.Add(path))
                {
                    continue;
                }

                using var roleReply = await CallAsync(address, application, "call", path, "GetRoleName").ConfigureAwait(false);
                using var nameReply = await CallAsync(address, application, "get-property", path, "Name").ConfigureAwait(false);
                var role = roleReply.RootElement.GetProperty("data")[0].GetString() ?? string.Empty;
                var spoken = (nameReply.RootElement.GetProperty("data").GetString() ?? string.Empty).Trim();
                Observe(state, role, spoken);
                using var childrenReply = await CallAsync(address, application, "call", path, "GetChildren").ConfigureAwait(false);
                var children = childrenReply.RootElement.GetProperty("data")[0];
                for (var index = children.GetArrayLength() - 1; index >= 0; index--)
                {
                    pending.Push(children[index][1].GetString() ?? throw new InvalidDataException("An accessible child has no object path."));
                }
            }

            return state;
        }

        /// <summary>Records windows and checks spoken control names.</summary>
        /// <param name="state">The inspection results.</param>
        /// <param name="role">The accessible role.</param>
        /// <param name="spoken">The trimmed spoken name.</param>
        private static void Observe(WalkState state, string role, string spoken)
        {
            if (role == "frame")
            {
                state.Frames.Add(spoken);
            }
            else if (Controls.Contains(role))
            {
                if (spoken.Length == 0 || spoken.StartsWith("Avalonia.", StringComparison.Ordinal))
                {
                    state.Bad.Add($"{role}: '{spoken}'");
                }
                else
                {
                    state.Named++;
                }
            }
        }

        /// <summary>Calls busctl with separately passed arguments.</summary>
        /// <param name="address">The accessibility bus address.</param>
        /// <param name="application">The application bus name.</param>
        /// <param name="command">The busctl operation.</param>
        /// <param name="path">The accessible object path.</param>
        /// <param name="member">The property or method name.</param>
        /// <returns>The reply owned by the caller.</returns>
        /// <exception cref="InvalidOperationException">busctl fails or returns no reply.</exception>
        private static async Task<JsonDocument> CallAsync(string address, string application, string command, string path, string member)
        {
            var result = await Process.RunAndCaptureTextAsync(
                "busctl",
                [$"--address={address}", "--json=short", "--timeout=3", command, application, path, Accessible, member]).ConfigureAwait(false);

            // A signal or a non-zero exit both mean busctl gave no usable reply.
            if (result.ExitStatus is not { ExitCode: 0, Signal: null } || string.IsNullOrWhiteSpace(result.StandardOutput) || !string.IsNullOrWhiteSpace(result.StandardError))
            {
                throw new InvalidOperationException($"busctl {member} failed: {result.StandardError}");
            }

            return JsonDocument.Parse(result.StandardOutput);
        }

        /// <summary>The accumulated accessibility inspection.</summary>
        private sealed class WalkState
        {
            /// <summary>Gets the spoken window names.</summary>
            internal List<string> Frames { get; } = [];

            /// <summary>Gets controls with invalid names.</summary>
            internal List<string> Bad { get; } = [];

            /// <summary>Gets or sets the count of correctly named controls.</summary>
            internal int Named { get; set; }
        }
    }
}
