using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Chasser.Common.Network;

namespace Chasser.Logic.Network
{
    public static class TCPClientManager
    {
        private static TcpClient _tcpClient;
        private static StreamWriter _writer;
        private static StreamReader _reader;
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions { PropertyNameCaseInsensitive = true };
        private static readonly SemaphoreSlim _streamLock = new SemaphoreSlim(1, 1);
        private static CancellationTokenSource _cts;

        public static bool IsConnected => _tcpClient?.Connected ?? false;

        public static event Action<ResponseMessage> OnMessageReceived;
        public static event Action OnDisconnected;

        public static async Task ConnectAsync(string host, int port)
        {
            _tcpClient = new TcpClient();
            await _tcpClient.ConnectAsync(host, port);
            var stream = _tcpClient.GetStream();
            _writer = new StreamWriter(stream) { AutoFlush = true };
            _reader = new StreamReader(stream);
            _cts = new CancellationTokenSource();
            _ = ListenAsync(_cts.Token); // fire and forget
        }

        public static async Task SendMessageAsync(RequestMessage message)
        {
            var json = JsonSerializer.Serialize(message);
            await _streamLock.WaitAsync();
            try
            {
                await _writer.WriteLineAsync(json);
            }
            finally
            {
                _streamLock.Release();
            }
        }

        private static async Task ListenAsync(CancellationToken cancellationToken)
        {
            try
            {
                while (!cancellationToken.IsCancellationRequested)
                {
                    string json;
                    await _streamLock.WaitAsync();
                    try
                    {
                        json = await _reader.ReadLineAsync();
                    }
                    finally
                    {
                        _streamLock.Release();
                    }

                    if (string.IsNullOrEmpty(json))
                    {
                        Disconnect();
                        return;
                    }

                    var response = JsonSerializer.Deserialize<ResponseMessage>(json, _jsonOptions);
                    OnMessageReceived?.Invoke(response);
                }
            }
            catch (Exception)
            {
                Disconnect();
            }
        }

        public static void Disconnect()
        {
            _cts?.Cancel();
            _tcpClient?.Close();
            _tcpClient = null;
            _writer = null;
            _reader = null;
            OnDisconnected?.Invoke();
        }
    }
}
