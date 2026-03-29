using System;
using System.Collections.Generic;
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Channels;
using System.Threading.Tasks;
using FenixFpm.Contracts.Interop;
using FenixFpm.Core.Abstractions;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.FlightSimulator.SimConnect;

namespace FenixFpm.Infrastructure.SharedMemory;

public sealed class FenixSharedMemoryReader : BackgroundService, IAsyncDisposable, ISimConnectService
{
    private const string DefaultClientDataName = "FENIX_FPM_DATA";
    private const int MaxClientDataPayloadSize = 4096;

    private readonly string _clientDataName;
    private readonly object _connectionSync = new();
    private readonly object _snapshotSync = new();
    private readonly object _logSync = new();
    private readonly Channel<FenixFpmSharedBuffer> _snapshotChannel;

    private SimConnect? _simConnect;
    private FenixFpmSharedBuffer _latestSnapshot;
    private bool _hasSnapshot;
    private int _startRequested;
    private int _channelCompleted;

    [ActivatorUtilitiesConstructor]
    public FenixSharedMemoryReader(Channel<FenixFpmSharedBuffer> snapshotChannel)
        : this(DefaultClientDataName, snapshotChannel)
    {
    }

    public FenixSharedMemoryReader(string clientDataName = DefaultClientDataName)
        : this(clientDataName, null)
    {
    }

    private FenixSharedMemoryReader(string clientDataName, Channel<FenixFpmSharedBuffer>? snapshotChannel)
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
        ConnectionStatus = "Disconnected";
    }

    public bool IsConnected { get; private set; }

    public string ConnectionStatus { get; private set; }

    public event Action ConnectionStateChanged = delegate { };

    public Channel<FenixFpmSharedBuffer> SnapshotChannel => _snapshotChannel;

    public override Task StartAsync(CancellationToken cancellationToken)
    {
        Interlocked.Exchange(ref _startRequested, 1);
        return base.StartAsync(cancellationToken);
    }

    public ValueTask InitializeAsync(CancellationToken cancellationToken = default)
    {
        if (Interlocked.CompareExchange(ref _startRequested, 1, 0) != 0)
        {
            return ValueTask.CompletedTask;
        }

        return new ValueTask(StartAsync(cancellationToken));
    }

    public void ForceConnect()
    {
        ValidateBufferSize();
        SetConnectionState(false, "Connecting...");
        DisposeSimConnect(DetachCurrentConnection());

        SimConnect? simConnect = null;

        try
        {
            simConnect = new SimConnect("FenixFpmReader", IntPtr.Zero, 0, null, 0);
            simConnect.OnRecvClientData += OnRecvClientData;
            simConnect.OnRecvQuit += OnRecvQuit;

            var clientDataSize = (uint)Marshal.SizeOf<FenixFpmSharedBuffer>();
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

            LogToFile("Connected to MSFS SimConnect client data stream.");
            SetConnectionState(true, "Connected to MSFS");
        }
        catch (COMException ex)
        {
            LogToFile($"ForceConnect COMException: 0x{ex.ErrorCode:X8} {ex.Message}");
            CleanupFailedConnection(simConnect);
            SetConnectionState(false, $"Connect failed: {ex.Message}");
        }
        catch (Exception ex)
        {
            LogToFile($"ForceConnect Error: {ex}");
            CleanupFailedConnection(simConnect);
            SetConnectionState(false, $"Connect failed: {ex.Message}");
        }
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

        if (!IsConnected)
        {
            ForceConnect();
        }

        await foreach (var snapshot in _snapshotChannel.Reader.ReadAllAsync(cancellationToken).ConfigureAwait(false))
        {
            yield return snapshot;
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        ValidateBufferSize();
        LogToFile("SimConnect background pump started.");

        while (!stoppingToken.IsCancellationRequested)
        {
            SimConnect? simConnect = null;

            lock (_connectionSync)
            {
                if (IsConnected && _simConnect != null)
                {
                    simConnect = _simConnect;
                }
            }

            if (IsConnected && simConnect != null)
            {
                try
                {
                    simConnect.ReceiveMessage();
                }
                catch (Exception ex)
                {
                    LogToFile($"Pump Error: {ex.Message}");
                    Disconnect("Disconnected - pump error");
                }
            }

            await Task.Delay(16, stoppingToken).ConfigureAwait(false);
        }

        LogToFile("SimConnect background pump stopping.");
        Disconnect("Disconnected");
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        Disconnect("Disconnected");
        await base.StopAsync(cancellationToken).ConfigureAwait(false);
        CompleteChannel();
    }

    public override void Dispose()
    {
        Disconnect("Disconnected");
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

    private void LogToFile(string msg)
    {
        try
        {
            lock (_logSync)
            {
                File.AppendAllText("SimConnect_Log.txt", $"{DateTime.Now:HH:mm:ss}: {msg}\n");
            }
        }
        catch
        {
        }
    }

    private void SetConnectionState(bool isConnected, string status)
    {
        var hasChanged = false;

        lock (_connectionSync)
        {
            if (IsConnected != isConnected || !string.Equals(ConnectionStatus, status, StringComparison.Ordinal))
            {
                IsConnected = isConnected;
                ConnectionStatus = status;
                hasChanged = true;
            }
        }

        if (hasChanged)
        {
            ConnectionStateChanged();
        }
    }

    private SimConnect? DetachCurrentConnection()
    {
        lock (_connectionSync)
        {
            var simConnect = _simConnect;
            _simConnect = null;
            return simConnect;
        }
    }

    private void CleanupFailedConnection(SimConnect? simConnect)
    {
        DisposeSimConnect(simConnect);
    }

    private void Disconnect(string status)
    {
        DisposeSimConnect(DetachCurrentConnection());
        SetConnectionState(false, status);
    }

    private void DisposeSimConnect(SimConnect? simConnect)
    {
        if (simConnect is null)
        {
            return;
        }

        try
        {
            simConnect.OnRecvClientData -= OnRecvClientData;
            simConnect.OnRecvQuit -= OnRecvQuit;
        }
        catch (Exception ex)
        {
            LogToFile($"Dispose unsubscribe error: {ex.Message}");
        }
        finally
        {
            try
            {
                simConnect.Dispose();
            }
            catch (Exception ex)
            {
                LogToFile($"Dispose error: {ex.Message}");
            }
        }
    }

    private unsafe void OnRecvClientData(SimConnect sender, SIMCONNECT_RECV_CLIENT_DATA data)
    {
        _ = sender;

        try
        {
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
                LogToFile("Checksum mismatch while receiving client data.");
                return;
            }

            lock (_snapshotSync)
            {
                _latestSnapshot = snapshot;
                _hasSnapshot = true;
            }

            _snapshotChannel.Writer.TryWrite(snapshot);
        }
        catch (Exception ex)
        {
            LogToFile($"OnRecvClientData Error: {ex}");
        }
    }

    private void OnRecvQuit(SimConnect sender, SIMCONNECT_RECV data)
    {
        _ = sender;
        _ = data;
        LogToFile("Simulator closed the SimConnect session.");
        Disconnect("MSFS closed the connection");
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
