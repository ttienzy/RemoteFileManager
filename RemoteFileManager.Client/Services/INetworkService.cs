using RemoteFileManager.Shared.Models.Network;
using System;
using System.Collections.Generic;
using System.Text;

namespace RemoteFileManager.Client.Services
{
    public interface INetworkService
    {
        event Action<Packet> PacketReceived; // Sự kiện khi nhận được gói tin
        event Action OnDisconnected;         // Sự kiện khi mất mạng
        bool IsConnected { get; }
        Task<bool> ConnectAsync(string ip, int port);
        Task SendPacketAsync(Packet packet);
        void Disconnect();
    }
}
