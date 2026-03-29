using System;

namespace FenixFpm.Core.Abstractions;

public interface ISimConnectService
{
    bool IsConnected { get; }
    string ConnectionStatus { get; }
    event Action ConnectionStateChanged;
    void ForceConnect();
}
