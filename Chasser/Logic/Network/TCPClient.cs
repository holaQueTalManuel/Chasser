using System;
using System.Net.Sockets;
using System.IO;
using System.Threading.Tasks;
using System.Text.Json;
using Chasser.Common.Network;

namespace Chasser.Logic.Network
{
    public static class TCPClient
    {
        private static TcpClient _client;
        private static NetworkStream _stream;
        private static StreamReader _reader;
        private static StreamWriter _writer;
        private static readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions
        {
            PropertyNameCaseInsensitive = true,
            WriteIndented = false
        };
        private static readonly SemaphoreSlim streamLock = new SemaphoreSlim(1, 3);
        public static bool IsConnected => _client?.Connected == true;

        public static async Task ConnectAsync(string ip, int port, int timeout = 5000)
        {
            try
            {
                _client = new TcpClient();
                var connectTask = _client.ConnectAsync(ip, port);

                // Añadir timeout
                if (await Task.WhenAny(connectTask, Task.Delay(timeout)) != connectTask)
                {
                    throw new TimeoutException("Tiempo de conexión excedido");
                }

                _stream = _client.GetStream();
                _reader = new StreamReader(_stream);
                _writer = new StreamWriter(_stream) { AutoFlush = true };
            }
            catch (Exception ex)
            {
                Disconnect();
                throw new Exception($"Error al conectar con {ip}:{port}", ex);
            }
        }

        public static async Task SendOnlyMessageAsync(RequestMessage message)
        {
            if (!IsConnected)
                throw new InvalidOperationException("Cliente no conectado al servidor");

            try
            {
                //await streamLock.WaitAsync();
                string json = JsonSerializer.Serialize(message, _jsonOptions);
                await _writer.WriteLineAsync(json);
                await _writer.FlushAsync();
            }
            catch (Exception ex)
            {
                Disconnect();
                throw new Exception("Error al enviar mensaje al servidor", ex);
            }
            finally
            {
                //streamLock.Release();
            }
        }


        public static async Task<ResponseMessage> ReceiveMessageAsync()
        {
            if (!IsConnected)
                throw new InvalidOperationException("Cliente no conectado al servidor");

            try
            {
                //await streamLock.WaitAsync(); // ⚠️ IMPORTANTE
                string responseJson = await _reader.ReadLineAsync();

                if (string.IsNullOrEmpty(responseJson))
                {
                    throw new Exception("El servidor cerró la conexión");
                }

                return JsonSerializer.Deserialize<ResponseMessage>(responseJson, _jsonOptions);
            }
            catch (Exception ex)
            {
                Disconnect();
                throw new Exception("Error al recibir mensaje del servidor", ex);
            }
            finally
            {
                //streamLock.Release(); // ⚠️ IMPORTANTE
            }
        }
        public static async Task<string> ReceiveMessageAsyncRaw()
        {
            if (!IsConnected)
                throw new InvalidOperationException("Cliente no conectado al servidor");

            try
            {
                string responseJson = await _reader.ReadLineAsync();

                if (string.IsNullOrEmpty(responseJson))
                {
                    throw new Exception("El servidor cerró la conexión");
                }

                return responseJson;
            }
            catch (Exception ex)
            {
                Disconnect();
                throw new Exception("Error al recibir mensaje del servidor", ex);
            }
        }




        public static void Disconnect()
        {
            try
            {
                _writer?.Dispose();
                _reader?.Dispose();
                _stream?.Dispose();
                _client?.Dispose();
            }
            catch
            {
                // Silenciar errores de disposición
            }
            finally
            {
                _writer = null;
                _reader = null;
                _stream = null;
                _client = null;
            }
        }
    }
}