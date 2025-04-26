using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using Chasser.Model;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Chasser.Logic
{
    class TCPServer
    {
        private static TcpListener listener;
        private readonly ChasserContext _context;
        public event Action<string> ClientConnected;
        public event Action<string, string> MessageReceived;

        public async Task StartAsync(int port)
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                //extrae la ip y se la pasa al evento
                ClientConnected?.Invoke(((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString());
                _ = HandleClientAsync(client);
            }
        }
        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using (var stream = client.GetStream())
                using (var reader = new StreamReader(stream))
                {
                    while (client.Connected)
                    {
                        string message = await reader.ReadLineAsync();
                        if (message == null)
                            break; // Cliente cerró la conexión

                        MessageReceived?.Invoke(
                            ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString(),
                            message);

                        switch (message)
                        {
                            case message.StartsWith("REGISTER|"):
                                string[] parts = message.Split('|');
                                string username = parts[1];
                                string password = parts[2];
                                string email = parts[3];

                                bool success = await RegisterUser(username, password, email);

                                // Responde al cliente
                                await writer.WriteLineAsync(success ? "REGISTER_SUCCESS" : "REGISTER_FAIL");
                                await writer.FlushAsync();
                        }
                    }
                }
            }
            catch (IOException ex)
            {
                Debug.WriteLine($"Error de lectura: {ex.Message}");
            }
            catch (SocketException ex)
            {
                Debug.WriteLine($"Error de socket: {ex.Message}");
            }
            finally
            {
                client.Dispose(); // Cierra la conexión
            }
        }
        private async Task<bool> RegisterUser(string username, string password, string email)
        {
            using (var context = App.ServiceProvider.GetRequiredService<ChasserContext>())
            {
                if (context.Usuarios.Any(u => u.Nombre == username))
                    return false; // Usuario ya existe

                string hasheadaHistorica = BCryptPasswordHasher.HashPassword(password);

                Usuario usu = new Usuario
                {
                    Nombre = username,
                    Correo = email,
                    Contrasenia = hasheadaHistorica,
                    Fecha_Creacion = DateTime.Now
                };

                _context.Usuarios.Add(usu);
                await context.SaveChangesAsync();
                return true;
            }
        }
    }
}
