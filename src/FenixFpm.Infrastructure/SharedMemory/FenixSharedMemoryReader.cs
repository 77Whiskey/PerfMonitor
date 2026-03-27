using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FenixFpm.Contracts.Interop;
using Microsoft.Extensions.Hosting;
using Microsoft.FlightSimulator.SimConnect;

namespace FenixFpm.Infrastructure.SharedMemory;

public sealed class FenixSharedMemoryReader : BackgroundService, IAsyncDisposable
{
    private const string DefaultClientDataName = "FENIX_FPM_DATA";
    private const int MaxClientDataPayloadSize = 4096;
    private static readonly TimeSpan ReconnectDelay = TimeSpan.FromSeconds(5);

    private readonly string _clientDataName;
    private readonly object _connectionSync = new();
    private readonly object _snapshotSync = new();
    private readonly Channel<FenixFpmSharedBuffer> _snapshotChannel;

    private SimConnect? _simConnect;
    private FenixFpmSharedBuffer _latestSnapshot;
    private bool _hasSnapshot;
    private int _startRequested;
    private int _channelCompleted;

    public FenixSharedMemoryReader(
        string clientDataName = DefaultClientDataName,
        Channel<FenixFpmSharedBuffer>? snapshotChannel = null)
    {
        if (!string.IsNullOrWhiteSpace(clientDataName) &&
            !string.Equals(clientDataName, DefaultClientDataName, StringComparison.Ordinal))
        {
            throw new ArgumentException(
                $"The WASM bridge publishes only to '{DefaultClientDataName}'.",
                nameof(clientDataName));
        }

        _clientDataName = DefaultClientDataName;
        _snapshotChannel = snapshotChannel ?? Channel.CreateUnbounded<FenixFpmSharedBuffer>(
            new UnboundedChannelOptions { SingleReader = false, SingleWriter = true });
    }

    public Channel<FenixFpmSharedBuffer> SnapshotChannel => _snapshotChannel;

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _startRequested, 1, 0) != 0)
        {
            return ValueTask.CompletedTask;
        }

        return new ValueTask(StartAsync(cancellationToken));
    }

    public bool TryRead(out FenixFpmSharedBuffer snapshot)
    {
        lock (_snapshotSync)
        {
            snapshot = _latestSnapshot;
            return _hasSnapshot;
        }
    }

    public async IAsyncEnumerable<FenixFpmSharedBuffer> ReadSnapshotsAsync(
        TimeSpan pollInterval,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _ = pollInterval;
        await InitializeAsync(cancellationToken).ConfigureAwait(false);

        await foreach (var snapshot in _snapshotChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return snapshot;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ValidateBufferSize();

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var simConnect = ConnectSimConnect();
                await RunDispatchLoopAsync(simConnect, stoppingToken).ConfigureAwait(false);
            }
            catch (COMException ex) when (!stoppingToken.IsCancellationRequested)
            {
                Trace.TraceWarning($"[SimConnect] Unable to connect to MSFS or client data stream. Retrying in {ReconnectDelay.TotalSeconds:F0}s. {ex.Message}");
                DisconnectSimConnect();
                await Task.Delay(ReconnectDelay, stoppingToken).ConfigureAwait(false);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                Trace.TraceWarning($"[SimConnect] Reader loop failed unexpectedly. Retrying in {ReconnectDelay.TotalSeconds:F0}s. {ex.Message}");
                DisconnectSimConnect();
                await Task.Delay(ReconnectDelay, stoppingToken).ConfigureAwait(false);
            }
        }

        DisconnectSimConnect();
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        DisconnectSimConnect();
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        CompleteChannel();
    }

    public override void Dispose()
    {
        DisconnectSimConnect();
        CompleteChannel();
        base.Dispose();
    }

    public async ValueTask DisposeAsync()
    {
        try
        {
            await StopAsync(CancellationToken.None).ConfigureAwait(false);
        }
        catch (ObjectDisposedException)
        {
        }
        catch (OperationCanceledException)
        {
        }

        Dispose();
        GC.SuppressFinalize(this);
    }

    private SimConnect ConnectSimConnect()
    {
        var clientDataSize = (uint)Marshal.SizeOf<FenixFpmSharedBuffer>();
        var simConnect = new SimConnect("FenixFpmReader", IntPtr.Zero, 0, null, 0);

        try
        {
            simConnect.OnRecvClientData += OnRecvClientData;
            simConnect.OnRecvQuit += OnRecvQuit;
            simConnect.MapClientDataNameToID(_clientDataName, ClientDataAreaId.FenixFpm);
            simConnect.AddToClientDataDefinition(ClientDataDefinition.Buffer, 0, clientDataSize, 0, 0);
            simConnect.RegisterStruct<SIMCONNECT_RECV_CLIENT_DATA, SimConnectClientDataPayload>(ClientDataDefinition.Buffer);
            simConnect.RequestClientData(
                ClientDataAreaId.FenixFpm,
                ClientDataRequest.Stream,
                ClientDataDefinition.Buffer,
                SIMCONNECT_CLIENT_DATA_PERIOD.VISUAL_FRAME,
                SIMCONNECT_CLIENT_DATA_REQUEST_FLAG.CHANGED,
                0,
                0,
                0);

            lock (_connectionSync)
            {
                _simConnect = simConnect;
            }

            return simConnect;
        }
        catch
        {
            simConnect.OnRecvClientData -= OnRecvClientData;
            simConnect.OnRecvQuit -= OnRecvQuit;
            simConnect.Dispose();
            throw;
        }
    }

    private async Task RunDispatchLoopAsync(SimConnect simConnect, CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            ProcessMessages(simConnect);

            lock (_connectionSync)
            {
                if (!ReferenceEquals(_simConnect, simConnect))
                {
                    return;
                }
            }

            await Task.Delay(16, stoppingToken).ConfigureAwait(false);
        }
    }

    private void DisconnectSimConnect()
    {
        SimConnect? simConnect;

        lock (_connectionSync)
        {
            simConnect = _simConnect;
            _simConnect = null;
        }

        if (simConnect is null)
        {
            return;
        }

        try
        {
            simConnect.OnRecvClientData -= OnRecvClientData;
            simConnect.OnRecvQuit -= OnRecvQuit;
        }
        finally
        {
            simConnect.Dispose();
        }
    }

    private void CompleteChannel()
    {
        if (Interlocked.Exchange(ref _channelCompleted, 1) == 0)
        {
            _snapshotChannel.Writer.TryComplete();
        }
    }

    private static void ValidateBufferSize()
    {
        var clientDataSize = Marshal.SizeOf<FenixFpmSharedBuffer>();
        if (clientDataSize > MaxClientDataPayloadSize)
        {
            throw new InvalidOperationException(
                $"FenixFpmSharedBuffer exceeds the registered SimConnect payload envelope ({MaxClientDataPayloadSize} bytes).");
        }
    }

    private static void ProcessMessages(SimConnect simConnect)
    {
        try
        {
            simConnect.ReceiveMessage();
        }
        catch (COMException ex) when (ex.ErrorCode == unchecked((int)0x80004005))
        {
        }
    }

    private unsafe void OnRecvClientData(SimConnect sender, SIMCONNECT_RECV_CLIENT_DATA data)
    {
        _ = sender;

        if (data.dwRequestID != (uint)ClientDataRequest.Stream || data.dwData.Length == 0)
        {
            return;
        }

        if (data.dwData[0] is not SimConnectClientDataPayload payload)
        {
            return;
        }

        var bufferSize = FenixSharedMemoryLayout.BufferSize;
        ReadOnlySpan<byte> payloadBytes = new(payload.Data, bufferSize);
        var snapshot = MemoryMarshal.Read<FenixFpmSharedBuffer>(payloadBytes);

        if (snapshot.Header.Version != FenixSharedMemoryLayout.Version ||
            snapshot.Header.SizeBytes != (uint)bufferSize)
        {
            return;
        }

        if (snapshot.Checksum != ComputeChecksum(snapshot))
        {
            return;
        }

        lock (_snapshotSync)
        {
            _latestSnapshot = snapshot;
            _hasSnapshot = true;
        }

        _snapshotChannel.Writer.TryWrite(snapshot);
    }

    private void OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
    {
        _ = sender;
        _ = data;
        Trace.TraceWarning("[SimConnect] Simulator closed the client data session. Waiting to reconnect.");
        DisconnectSimConnect();
    }

    private static uint ComputeChecksum(FenixFpmSharedBuffer snapshot)
    {
        uint checksum = 2166136261;
        var bytes = MemoryMarshal.AsBytes(MemoryMarshal.CreateReadOnlySpan(ref snapshot, 1));
        var length = bytes.Length - sizeof(uint);

        for (var index = 0; index < length; index++)
        {
            checksum ^= bytes[index];
            checksum *= 16777619;
        }

        return checksum;
    }

    private enum ClientDataAreaId
    {
        FenixFpm = 0x1100
    }

    private enum ClientDataDefinition
    {
        Buffer = 0x1100
    }

    private enum ClientDataRequest
    {
        Stream = 0x1100
    }

    [StructLayout(LayoutKind.Sequential, Pack = 1, Size = MaxClientDataPayloadSize)]
    private unsafe struct SimConnectClientDataPayload
    {
        public fixed byte Data[MaxClientDataPayloadSize];
    }
}
