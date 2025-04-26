using System;
using System.Net.Sockets;
using System.IO;
using System.Threading.Tasks;

namespace Chasser.Logic
{
    public static class TCPClient
    {
        private static TcpClient _client;
        private static NetworkStream _stream;
        private static StreamReader _reader;
        private static StreamWriter _writer;

        // Conexión inicial (llamar al iniciar la aplicación)
        public static async Task ConnectAsync(string ip, int port)
        {
            _client = new TcpClient();
            await _client.ConnectAsync(ip, port);
            _stream = _client.GetStream();
            _reader = new StreamReader(_stream);
            _writer = new StreamWriter(_stream) { AutoFlush = true };
        }

        // Envía mensajes y espera respuesta (genérico)
        public static async Task<string> SendMessageAsync(string message)
        {
            if (_client == null || !_client.Connected)
                throw new InvalidOperationException("Cliente no conectado");

            try
            {
                await _writer.WriteLineAsync(message);
                return await _reader.ReadLineAsync();
            }
            catch (Exception ex)
            {
                Disconnect();
                throw new Exception("Error al enviar mensaje", ex);
            }
        }

        public static void Disconnect()
        {
            _reader?.Dispose();
            _writer?.Dispose();
            _stream?.Dispose();
            _client?.Dispose();
        }
    }
}