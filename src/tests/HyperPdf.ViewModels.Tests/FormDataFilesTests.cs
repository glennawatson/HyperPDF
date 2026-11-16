// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Security.Cryptography;
using HyperPdf.ViewModels.FormFilling;
using ReactiveUI.Primitives;

namespace HyperPdf.ViewModels.Tests;

/// <summary>Checks the file read of a form's Import Data action, which treats the file as untrusted.</summary>
public sealed class FormDataFilesTests
{
    /// <summary>The bytes of the sample file.</summary>
    private const int SampleLength = 2048;

    /// <summary>A file the reader chose is read whole.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task ReadsTheChosenFile()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-fdf-{Guid.NewGuid():N}.fdf");
        var bytes = new byte[SampleLength];
        RandomNumberGenerator.Fill(bytes);
        await File.WriteAllBytesAsync(path, bytes);
        try
        {
            var read = await FormDataFiles.Read(path).ToTask();

            await Assert.That(read).IsEquivalentTo(bytes);
        }
        finally
        {
            File.Delete(path);
        }
    }

    /// <summary>A missing file gives nothing rather than an error.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task MissingFileGivesNothing()
    {
        var read = await FormDataFiles.Read(Path.Combine(Path.GetTempPath(), $"hyperpdf-missing-{Guid.NewGuid():N}.fdf")).ToTask();

        await Assert.That(read).IsNull();
    }

    /// <summary>A file longer than the limit is refused without being read.</summary>
    /// <returns>A task.</returns>
    [Test]
    public async Task TooLongFileGivesNothing()
    {
        var path = Path.Combine(Path.GetTempPath(), $"hyperpdf-long-{Guid.NewGuid():N}.fdf");
        await using (var stream = new FileStream(path, FileMode.Create, FileAccess.Write))
        {
            stream.SetLength(FormDataFiles.MaxBytes + 1);
        }

        try
        {
            var read = await FormDataFiles.Read(path).ToTask();

            await Assert.That(read).IsNull();
        }
        finally
        {
            File.Delete(path);
        }
    }
}
