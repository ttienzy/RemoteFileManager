using RemoteFileManager.Shared.Models.Network;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;

namespace RemoteFileManager.Shared.Helpers
{
    public static class SocketHelper
    {
        // 1. Gửi Packet
        public static async Task SendPacketAsync(NetworkStream stream, Packet packet)
        {
            try
            {
                string json = JsonSerializer.Serialize(packet);
                byte[] data = Encoding.UTF8.GetBytes(json);

                // Protocol: [4 bytes Length][Data]
                byte[] lengthBytes = BitConverter.GetBytes(data.Length);

                // Gộp mảng để gửi 1 lần (Tối ưu TCP)
                byte[] finalPacket = new byte[4 + data.Length];
                Buffer.BlockCopy(lengthBytes, 0, finalPacket, 0, 4);
                Buffer.BlockCopy(data, 0, finalPacket, 4, data.Length);

                await stream.WriteAsync(finalPacket, 0, finalPacket.Length);
                await stream.FlushAsync();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error Sending: {ex.Message}");
                // Xử lý logic ngắt kết nối tại đây nếu cần
                throw;
            }
        }

        // 2. Nhận Packet
        public static async Task<Packet?> ReceivePacketAsync(NetworkStream stream)
        {
            try
            {
                // Bước 1: Đọc 4 byte độ dài
                byte[] lengthBuffer = new byte[4];
                int bytesRead = await ReadExactlyAsync(stream, lengthBuffer, 4);

                if (bytesRead == 0) return null; // Client đã ngắt kết nối

                int dataSize = BitConverter.ToInt32(lengthBuffer, 0);
                if (dataSize < 0 || dataSize > 100 * 1024 * 1024)
                    throw new Exception("Invalid packet size (Possible attack or corruption)");

                // Bước 2: Đọc nội dung dựa trên độ dài
                byte[] dataBuffer = new byte[dataSize];
                bytesRead = await ReadExactlyAsync(stream, dataBuffer, dataSize);

                if (bytesRead != dataSize) return null;

                // Bước 3: Deserialize
                string json = Encoding.UTF8.GetString(dataBuffer);
                return JsonSerializer.Deserialize<Packet>(json);
            }
            catch
            {
                return null;
            }
        }

        // Helper đọc đủ số byte yêu cầu
        private static async Task<int> ReadExactlyAsync(NetworkStream stream, byte[] buffer, int size)
        {
            int totalRead = 0;
            while (totalRead < size)
            {
                int read = await stream.ReadAsync(buffer, totalRead, size - totalRead);
                if (read == 0) return 0;
                totalRead += read;
            }
            return totalRead;
        }
    }
}
