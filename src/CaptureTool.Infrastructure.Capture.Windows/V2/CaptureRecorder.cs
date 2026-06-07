using CaptureTool.Domain.Capture.V2;
using System.Runtime.InteropServices;

namespace CaptureTool.Infrastructure.Capture.Windows.V2;

public sealed class CaptureRecorder : IAsyncDisposable
{
    private readonly SemaphoreSlim _commandLock = new(1, 1);
    private readonly object _callbackRegistrationLock = new();
    private readonly List<CaptureCallbackRegistration> _callbackRegistrations = [];
    private CaptureRecorderSafeHandle _handle;
    private CaptureRecorderState _state = CaptureRecorderState.Idle;
    private bool _disposed;

    public CaptureRecorder()
    {
        int result = CaptureV2NativeMethods.CtCaptureV2_CreateRecorder(out _handle);
        if (result != (int)CaptureV2ResultCode.Success)
        {
            throw new CaptureNativeException(
                (CaptureV2ResultCode)result,
                nativeStatus: 0,
                component: "CaptureInteropV2Recorder",
                operation: "CreateRecorder",
                stage: 0,
                message: "Failed to create a native CaptureInterop V2 recorder.");
        }
    }

    public async Task<CaptureCallbackRegistration> RegisterCallbackAsync(
        Action<CaptureRecorderEvent> callback,
        ulong eventMask = CaptureRecorderEventMasks.All,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(callback);

        return await ExecuteAsync(
            () =>
            {
                CaptureV2NativeMethods.CaptureV2NativeEventCallback nativeCallback =
                    (eventData, _) => DispatchCallback(eventData, callback);

                CaptureV2NativeCallbackConfig config = new()
                {
                    Size = (uint)Marshal.SizeOf<CaptureV2NativeCallbackConfig>(),
                    Version = CaptureV2NativeMapping.DtoVersion,
                    EventCallback = Marshal.GetFunctionPointerForDelegate(nativeCallback),
                    EventMask = eventMask,
                };

                int result = CaptureV2NativeMethods.CtCaptureV2_RegisterCallbacks(
                    _handle,
                    in config,
                    out nint registrationHandle);
                CaptureV2ResultTranslator.ThrowIfFailed(_handle, result);

                var safeHandle = new CaptureCallbackRegistrationSafeHandle(registrationHandle, nativeCallback);
                var registration = new CaptureCallbackRegistration(safeHandle, RemoveCallbackRegistration);
                lock (_callbackRegistrationLock)
                {
                    _callbackRegistrations.Add(registration);
                }

                return registration;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task StartAsync(
        CapturePipelineOptions options,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(options);
        await ExecuteAsync(
            () =>
            {
                using CaptureV2ConfigMarshalScope marshalScope = CaptureV2ConfigMarshalScope.FromOptions(options);
                CaptureV2NativeConfig config = marshalScope.Config;
                int result = CaptureV2NativeMethods.CtCaptureV2_Start(_handle, in config);
                CaptureV2ResultTranslator.ThrowIfFailed(_handle, result);
                _state = CaptureRecorderState.Recording;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task PauseAsync(CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(
            () =>
            {
                int result = CaptureV2NativeMethods.CtCaptureV2_Pause(_handle);
                CaptureV2ResultTranslator.ThrowIfFailed(_handle, result);
                _state = CaptureRecorderState.Paused;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task ResumeAsync(CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(
            () =>
            {
                int result = CaptureV2NativeMethods.CtCaptureV2_Resume(_handle);
                CaptureV2ResultTranslator.ThrowIfFailed(_handle, result);
                _state = CaptureRecorderState.Recording;
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task SetAudioMutedAsync(
        CaptureSourceId sourceId,
        bool muted,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(
            () =>
            {
                int result = CaptureV2NativeMethods.CtCaptureV2_SetAudioMuted(
                    _handle,
                    sourceId.Value,
                    muted ? (byte)1 : (byte)0);
                CaptureV2ResultTranslator.ThrowIfFailed(_handle, result);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task SetAudioGainAsync(
        CaptureSourceId sourceId,
        float gainDb,
        CancellationToken cancellationToken = default)
    {
        await ExecuteAsync(
            () =>
            {
                int result = CaptureV2NativeMethods.CtCaptureV2_SetAudioGain(_handle, sourceId.Value, gainDb);
                CaptureV2ResultTranslator.ThrowIfFailed(_handle, result);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async Task<CaptureStopResult> StopAsync(CancellationToken cancellationToken = default)
    {
        return await ExecuteAsync(
            () =>
            {
                DisposeCallbackRegistrations();

                if (_state == CaptureRecorderState.Idle)
                {
                    return new CaptureStopResult
                    {
                        ResultCode = CaptureV2ResultCode.AlreadyStopped,
                    };
                }

                int result = CaptureV2NativeMethods.CtCaptureV2_Stop(_handle, out CaptureV2NativeStopResult stopResult);
                if (result != (int)CaptureV2ResultCode.Success
                    && result != (int)CaptureV2ResultCode.AlreadyStopped)
                {
                    CaptureV2ResultTranslator.ThrowIfFailed(_handle, result);
                }

                _state = CaptureRecorderState.Idle;
                return CaptureStopResult.FromNative(stopResult);
            },
            cancellationToken).ConfigureAwait(false);
    }

    public async ValueTask DisposeAsync()
    {
        if (_disposed)
        {
            return;
        }

        await _commandLock.WaitAsync().ConfigureAwait(false);
        try
        {
            if (_disposed)
            {
                return;
            }

            DisposeCallbackRegistrations();

            if (_state != CaptureRecorderState.Idle)
            {
                int stopResult = CaptureV2NativeMethods.CtCaptureV2_Stop(_handle, out _);
                if (stopResult != (int)CaptureV2ResultCode.Success
                    && stopResult != (int)CaptureV2ResultCode.AlreadyStopped)
                {
                    CaptureV2ResultTranslator.ThrowIfFailed(_handle, stopResult);
                }
            }

            _handle.Dispose();
            _disposed = true;
            _state = CaptureRecorderState.Disposed;
        }
        finally
        {
            _commandLock.Release();
            _commandLock.Dispose();
        }
    }

    internal async Task TestTriggerEventAsync(
        CaptureRecorderEvent eventData,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(eventData);
        await ExecuteAsync(
            () =>
            {
                CaptureV2NativeEvent nativeEvent = eventData.ToNative();
                int result = CaptureV2NativeMethods.CtCaptureV2_TestTriggerEvent(_handle, in nativeEvent);
                CaptureV2ResultTranslator.ThrowIfFailed(_handle, result);
            },
            cancellationToken).ConfigureAwait(false);
    }

    private async Task ExecuteAsync(Action command, CancellationToken cancellationToken)
    {
        await _commandLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            await Task.Run(command).ConfigureAwait(false);
        }
        finally
        {
            _commandLock.Release();
        }
    }

    private async Task<T> ExecuteAsync<T>(Func<T> command, CancellationToken cancellationToken)
    {
        await _commandLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            ThrowIfDisposed();
            cancellationToken.ThrowIfCancellationRequested();
            return await Task.Run(command).ConfigureAwait(false);
        }
        finally
        {
            _commandLock.Release();
        }
    }

    private void ThrowIfDisposed()
    {
        ObjectDisposedException.ThrowIf(_disposed, this);
    }

    private void RemoveCallbackRegistration(CaptureCallbackRegistration registration)
    {
        lock (_callbackRegistrationLock)
        {
            _callbackRegistrations.Remove(registration);
        }
    }

    private void DisposeCallbackRegistrations()
    {
        CaptureCallbackRegistration[] registrations;
        lock (_callbackRegistrationLock)
        {
            registrations = [.. _callbackRegistrations];
            _callbackRegistrations.Clear();
        }

        foreach (CaptureCallbackRegistration registration in registrations)
        {
            registration.DisposeFromOwner();
        }
    }

    private static void DispatchCallback(nint eventData, Action<CaptureRecorderEvent> callback)
    {
        try
        {
            if (eventData == 0)
            {
                return;
            }

            CaptureV2NativeEvent nativeEvent = Marshal.PtrToStructure<CaptureV2NativeEvent>(eventData);
            callback(CaptureRecorderEvent.FromNative(nativeEvent));
        }
        catch
        {
        }
    }

    private enum CaptureRecorderState
    {
        Idle,
        Recording,
        Paused,
        Disposed,
    }
}
