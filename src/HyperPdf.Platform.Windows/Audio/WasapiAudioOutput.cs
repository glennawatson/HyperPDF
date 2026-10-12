// Copyright (c) 2026 Glenn Watson. All rights reserved.
// Glenn Watson licenses this file to you under the MIT license.
// See the LICENSE file in the project root for full license information.

using System.Diagnostics;
using HyperPdf.Core.Speech;

namespace HyperPdf.Platform.Windows.Audio;

/// <summary>
/// Plays speech through WASAPI on the default output device in shared mode. Windows converts the 24 kHz float samples
/// to the device's format. The COM interfaces are called through their function tables, so nothing needs reflection.
/// Each clip opens a client on the current default device, so a change of headphones is followed.
/// </summary>
[DebuggerDisplay("WasapiAudioOutput: WASAPI")]
public sealed unsafe class WasapiAudioOutput : IAudioOutput
{
    /// <summary>Shared mode.</summary>
    private const int SharedMode = 0;

    /// <summary>Lets Windows convert the sample rate and format: AUTOCONVERTPCM | SRC_DEFAULT_QUALITY.</summary>
    private const uint ConvertFlags = 0x8000_0000 | 0x0800_0000;

    /// <summary>The buffer length, 200 ms in 100 ns units.</summary>
    private const long BufferDuration = 2_000_000;

    /// <summary>All COM server contexts.</summary>
    private const uint AllContexts = 0x17;

    /// <summary>The multithreaded COM apartment.</summary>
    private const uint MultiThreaded = 0;

    /// <summary>The render (playback) data flow.</summary>
    private const int Render = 0;

    /// <summary>The console device role.</summary>
    private const int Console = 0;

    /// <summary>The IUnknown::Release slot.</summary>
    private const int ReleaseSlot = 2;

    /// <summary>The IMMDeviceEnumerator::GetDefaultAudioEndpoint slot.</summary>
    private const int GetDefaultEndpointSlot = 4;

    /// <summary>The IMMDevice::Activate slot.</summary>
    private const int ActivateSlot = 3;

    /// <summary>The IAudioClient::Initialize slot.</summary>
    private const int InitializeSlot = 3;

    /// <summary>The IAudioClient::GetBufferSize slot.</summary>
    private const int GetBufferSizeSlot = 4;

    /// <summary>The IAudioClient::GetCurrentPadding slot.</summary>
    private const int GetCurrentPaddingSlot = 6;

    /// <summary>The IAudioClient::Start slot.</summary>
    private const int StartSlot = 10;

    /// <summary>The IAudioClient::Stop slot.</summary>
    private const int StopSlot = 11;

    /// <summary>The IAudioClient::Reset slot.</summary>
    private const int ResetSlot = 12;

    /// <summary>The IAudioClient::GetService slot.</summary>
    private const int GetServiceSlot = 14;

    /// <summary>The IAudioRenderClient::GetBuffer slot.</summary>
    private const int GetBufferSlot = 3;

    /// <summary>The IAudioRenderClient::ReleaseBuffer slot.</summary>
    private const int ReleaseBufferSlot = 4;

    /// <summary>The HRESULT returned when COM was already initialised in another apartment.</summary>
    private const int ChangedMode = unchecked((int)0x8001_0106);

    /// <summary>How long to wait for the device to want more audio.</summary>
    private static readonly TimeSpan PollInterval = TimeSpan.FromMilliseconds(10);

    /// <summary>The class id of the device enumerator, CLSID_MMDeviceEnumerator.</summary>
    private static readonly Guid DeviceEnumeratorClass = new("BCDE0395-E52F-467C-8E3D-C4579291692E");

    /// <summary>The interface id of IMMDeviceEnumerator.</summary>
    private static readonly Guid DeviceEnumeratorInterface = new("A95664D2-9614-4F35-A746-DE8DB63617E6");

    /// <summary>The interface id of IAudioClient.</summary>
    private static readonly Guid AudioClientInterface = new("1CB9AD4C-DBFA-4C32-B178-C2F568A703B2");

    /// <summary>The interface id of IAudioRenderClient.</summary>
    private static readonly Guid RenderClientInterface = new("F294ACFC-3146-4483-A7BF-ADDCA7C260E2");

    /// <summary>Serialises playback.</summary>
    private readonly Lock _gate = new();

    /// <summary>Gets a value indicating whether there is a default output device.</summary>
    public bool IsAvailable
    {
        get
        {
            var initialized = NativeMethods.CoInitializeEx(0, MultiThreaded);
            try
            {
                var device = OpenDefaultDevice();
                Release(device);
                return device is not null;
            }
            finally
            {
                Uninitialize(initialized);
            }
        }
    }

    /// <inheritdoc/>
    public Task PlayAsync(SpeechAudio audio, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(audio);

        // Playback waits for the clip to be heard, so it gets its own thread rather than holding a pool thread.
        return Task.Factory.StartNew(
            static state =>
            {
                var (output, clip, token) = ((WasapiAudioOutput, SpeechAudio, CancellationToken))state!;
                output.Play(clip, token);
            },
            (this, audio, cancellationToken),
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default);
    }

    /// <inheritdoc/>
    public void Dispose()
    {
        // Each clip releases its own client.
    }

    /// <summary>Gets a function from a COM object's table.</summary>
    /// <param name="instance">The object.</param>
    /// <param name="slot">The slot.</param>
    /// <returns>The function.</returns>
    [System.Runtime.CompilerServices.MethodImpl(System.Runtime.CompilerServices.MethodImplOptions.AggressiveInlining)]
    private static void* Slot(void* instance, int slot) => (*(void***)instance)[slot];

    /// <summary>Releases a COM object.</summary>
    /// <param name="instance">The object, or null.</param>
    private static void Release(void* instance)
    {
        if (instance is not null)
        {
            _ = ((delegate* unmanaged[Stdcall]<void*, uint>)Slot(instance, ReleaseSlot))(instance);
        }
    }

    /// <summary>Balances a successful <c>CoInitializeEx</c>.</summary>
    /// <param name="result">Its result.</param>
    private static void Uninitialize(int result)
    {
        if (result >= 0)
        {
            NativeMethods.CoUninitialize();
        }
    }

    /// <summary>Opens the default output device. Callers have initialised COM.</summary>
    /// <returns>The IMMDevice, or null when there is none.</returns>
    private static void* OpenDefaultDevice()
    {
        void* enumerator = null;
        var classId = DeviceEnumeratorClass;
        var interfaceId = DeviceEnumeratorInterface;
        _ = &interfaceId;
        if (NativeMethods.CoCreateInstance(&classId, null, AllContexts, &interfaceId, &enumerator) < 0)
        {
            return null;
        }

        try
        {
            void* device = null;
            var result = ((delegate* unmanaged[Stdcall]<void*, int, int, void**, int>)Slot(enumerator, GetDefaultEndpointSlot))(enumerator, Render, Console, &device);
            return result < 0 ? null : device;
        }
        finally
        {
            Release(enumerator);
        }
    }

    /// <summary>Opens an audio client for float samples at a rate. Callers have initialised COM.</summary>
    /// <param name="rate">The sample rate.</param>
    /// <param name="frames">Receives the buffer size in frames.</param>
    /// <returns>The IAudioClient, or null.</returns>
    private static void* OpenClient(int rate, out uint frames)
    {
        frames = 0;
        var device = OpenDefaultDevice();
        if (device is null)
        {
            return null;
        }

        void* client = null;
        try
        {
            var interfaceId = AudioClientInterface;
            if (((delegate* unmanaged[Stdcall]<void*, Guid*, uint, void*, void**, int>)Slot(device, ActivateSlot))(device, &interfaceId, AllContexts, null, &client) < 0)
            {
                return null;
            }

            var format = WaveFormat.MonoFloat(rate);
            var initialize = (delegate* unmanaged[Stdcall]<void*, int, uint, long, long, WaveFormat*, Guid*, int>)Slot(client, InitializeSlot);
            uint size = 0;
            if (initialize(client, SharedMode, ConvertFlags, BufferDuration, 0, &format, null) < 0
                || ((delegate* unmanaged[Stdcall]<void*, uint*, int>)Slot(client, GetBufferSizeSlot))(client, &size) < 0)
            {
                Release(client);
                return null;
            }

            frames = size;
            return client;
        }
        finally
        {
            Release(device);
        }
    }

    /// <summary>Gets the frames queued and not yet played.</summary>
    /// <param name="client">The IAudioClient.</param>
    /// <returns>The frames, or zero on error.</returns>
    private static uint Padding(void* client)
    {
        uint padding = 0;
        return ((delegate* unmanaged[Stdcall]<void*, uint*, int>)Slot(client, GetCurrentPaddingSlot))(client, &padding) < 0 ? 0 : padding;
    }

    /// <summary>Calls a method taking only the object, such as Start, Stop or Reset.</summary>
    /// <param name="client">The IAudioClient.</param>
    /// <param name="slot">The slot.</param>
    private static void Call(void* client, int slot) => _ = ((delegate* unmanaged[Stdcall]<void*, int>)Slot(client, slot))(client);

    /// <summary>Writes as many samples as the device has room for.</summary>
    /// <param name="client">The IAudioClient.</param>
    /// <param name="render">The IAudioRenderClient.</param>
    /// <param name="frames">The buffer size in frames.</param>
    /// <param name="samples">The samples left.</param>
    /// <returns>The samples written.</returns>
    private static int Fill(void* client, void* render, uint frames, ReadOnlySpan<float> samples)
    {
        var room = (int)Math.Min(frames - Padding(client), (uint)samples.Length);
        if (room <= 0)
        {
            return 0;
        }

        byte* buffer = null;
        if (((delegate* unmanaged[Stdcall]<void*, uint, byte**, int>)Slot(render, GetBufferSlot))(render, (uint)room, &buffer) < 0)
        {
            return 0;
        }

        samples[..room].CopyTo(new(buffer, room));
        _ = ((delegate* unmanaged[Stdcall]<void*, uint, uint, int>)Slot(render, ReleaseBufferSlot))(render, (uint)room, 0);
        return room;
    }

    /// <summary>Feeds the device until every sample is queued and played, or until cancelled.</summary>
    /// <param name="client">The IAudioClient.</param>
    /// <param name="render">The IAudioRenderClient.</param>
    /// <param name="frames">The buffer size in frames.</param>
    /// <param name="samples">The samples.</param>
    /// <param name="cancellationToken">The cancellation.</param>
    private static void Stream(void* client, void* render, uint frames, float[] samples, CancellationToken cancellationToken)
    {
        var written = Fill(client, render, frames, samples);
        Call(client, StartSlot);
        while (written < samples.Length || Padding(client) != 0)
        {
            if (cancellationToken.WaitHandle.WaitOne(PollInterval))
            {
                break;
            }

            if (written < samples.Length)
            {
                written += Fill(client, render, frames, samples.AsSpan(written));
            }
        }

        Call(client, StopSlot);
        Call(client, ResetSlot);
    }

    /// <summary>Plays the audio, then waits for the device to finish it.</summary>
    /// <param name="audio">The audio.</param>
    /// <param name="cancellationToken">Stops at once, discarding what is queued.</param>
    private void Play(SpeechAudio audio, CancellationToken cancellationToken)
    {
        lock (_gate)
        {
            var initialized = NativeMethods.CoInitializeEx(0, MultiThreaded);
            if (initialized < 0 && initialized != ChangedMode)
            {
                return;
            }

            var client = OpenClient(audio.SampleRate, out var frames);
            void* render = null;
            try
            {
                var interfaceId = RenderClientInterface;
                if (client is null || ((delegate* unmanaged[Stdcall]<void*, Guid*, void**, int>)Slot(client, GetServiceSlot))(client, &interfaceId, &render) < 0)
                {
                    return;
                }

                Stream(client, render, frames, audio.Samples, cancellationToken);
            }
            finally
            {
                Release(render);
                Release(client);
                Uninitialize(initialized);
            }
        }
    }
}
