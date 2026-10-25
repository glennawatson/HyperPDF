// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Reflection;
using System.Runtime.InteropServices;
using System.Runtime.Loader;
using HyperPdf.Core.Platform;

namespace HyperPdf.Core.Tests.Platform;

/// <summary>Tests that registering a native library installs the import resolver before any caller returns.</summary>
public sealed class NativeLibrariesTests
{
    /// <summary>The assemblies registered by several threads at once.</summary>
    private const int Trials = 200;

    /// <summary>The threads that register the same assembly.</summary>
    private const int Registrations = 4;

    /// <summary>The library name used in the registration.</summary>
    private const string Library = "hyperpdf-test-library";

    /// <summary>A caller that registers a library for an assembly is never returned to before the resolver for that assembly is installed.</summary>
    /// <param name="cancellationToken">Cancels the test.</param>
    /// <returns>A task.</returns>
    [Test]
    public async Task ResolverIsInstalledWhenRegisterReturns(CancellationToken cancellationToken)
    {
        var path = typeof(NativeLibrariesTests).Assembly.Location;
        var early = 0;
        for (var trial = 0; trial < Trials; trial++)
        {
            // Each trial loads its own copy of this assembly, because an assembly takes one resolver for its life.
            var context = new AssemblyLoadContext($"native-libraries-{trial}", true);
            try
            {
                var assembly = context.LoadFromAssemblyPath(path);
                var start = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
                var tasks = new Task<bool>[Registrations];
                for (var i = 0; i < tasks.Length; i++)
                {
                    tasks[i] = Task.Run(() => RegisterAndCheckAsync(assembly, start.Task), cancellationToken);
                }

                start.SetResult();
                foreach (var installed in await Task.WhenAll(tasks))
                {
                    early += installed ? 0 : 1;
                }
            }
            finally
            {
                context.Unload();
            }
        }

        await Assert.That(early).IsEqualTo(0);
    }

    /// <summary>Registers a library and checks that the assembly already has its resolver.</summary>
    /// <param name="assembly">The assembly.</param>
    /// <param name="start">Releases every thread together.</param>
    /// <returns><see langword="true"/> when the assembly already had a resolver.</returns>
    private static async Task<bool> RegisterAndCheckAsync(Assembly assembly, Task start)
    {
        await start;
        NativeLibraries.Register(assembly, Library, [Library]);
        try
        {
            // Setting a second resolver fails once one is installed.
            NativeLibrary.SetDllImportResolver(assembly, static (_, _, _) => 0);
            return false;
        }
        catch (InvalidOperationException)
        {
            return true;
        }
    }
}
