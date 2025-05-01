using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Chasser.Common.Logic;
using Chasser.Common.Network;
using Chasser.Model;
using Chasser.Server.Network;
using Chasser.Utilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace Chasser.Logic.Network
{
    class TCPServer
    {
        private static TcpListener listener;
        private readonly ChasserContext _context;
        public event Action<string> ClientConnected;
        public event Action<string, string> MessageReceived;
        private string userPath = "user.txt";
        private static Queue<TcpClient> waitingClients = new();
        private TcpClient client;



        public TCPServer(ChasserContext context)
        {
            _context = context;
        }

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
                using (var writer = new StreamWriter(stream) { AutoFlush = true })
                {
                    while (client.Connected)
                    {
                        string message = await reader.ReadLineAsync();
                        if (message == null)
                            break; // Cliente cerró la conexión

                        MessageReceived?.Invoke(
                            ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString(),
                            message);

                        //string[] parts = message.Split('|');
                        RequestMessage msg = JsonSerializer.Deserialize<RequestMessage>(message);
                        if (msg == null) continue;

                        switch (msg.Command)
                        {
                            //manejar caso de registro
                            case "REGISTER":
                                await HandleRegister(msg.Data, writer);
                                break;
                            //manejar caso de login
                            case "LOGIN":
                                await HandleLogin(msg.Data, writer);
                                break;
                            case "START_GAME":
                                await GenerateGame(msg.Data, writer, client);
                                break;
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

        private async Task GenerateGame(Dictionary<string, string> data, StreamWriter writer, TcpClient client)
        {
            Usuario userId = null;
            //if (parts.Length < 1)
            //{
            //    await writer.WriteLineAsync("START_GAME_FAIL|Datos insuficientes");
            //    return;
            //}

            if (File.Exists(userPath))
            {
                string savedUser = File.ReadAllText(userPath);
                userId = _context.Usuarios.FirstOrDefault(u => u.Nombre == savedUser);

                if (userId == null)
                {
                    await SendJsonAsync(writer, "START_GAME_FAIL", "No se ha encontrado ningun usuario logueado");
                    return;
                }

            }

            int playerId = _context.Usuarios
                .Where(x => x.Id == userId.Id)
                .Select(y => y.Id)
                .FirstOrDefault();

            bool success = await CreateGame(writer, playerId);

            if (success)
            {
                while (waitingClients.Count > 0 && !client.Connected)
                {
                    waitingClients.Dequeue(); // Elimina clientes desconectados
                }
                if (waitingClients.Count < 2)
                {
                    waitingClients.Enqueue(client);
                    await SendJsonAsync(writer, "WAITING_FOR_OPPONENT", "Esperando a un segundo jugador...");
                }

                
                TcpClient opponent = waitingClients.Dequeue();
                new GameSession(client, opponent);
                await SendJsonAsync(writer, "START_GAME_SUCCESS", "Partida creada correctamente");
            }
            else
            {
                await SendJsonAsync(writer, "START_GAME_FAIL", "No se ha podido crear la partida\"");
            }
        }

        private async Task<bool> CreateGame(StreamWriter writer, int id)
        {
            string gameCod = GenerateCod();

            if (_context.Partidas.Any(p => p.Codigo == gameCod))
            {
                await SendJsonAsync(writer, "CREATE_GAME_FAIL", "Se ha generado un mismo codigo, vuelve a intentarlo");
                return false;
            }
            Partida newGame = new Partida
            {

                Ganador = "",
                Codigo = gameCod,
                Duracion = DateTime.Now,
                Fecha_Creacion = DateTime.Now
            };

            _context.Partidas.Add(newGame);
            await _context.SaveChangesAsync(); // Ahora newGame.Id tiene valor

            Partida_Jugador newPartidaJugador = new Partida_Jugador
            {
                PartidaId = newGame.Id,
                Jugador1 = id,
            };

            _context.Partidas_Jugadores.Add(newPartidaJugador);
            await _context.SaveChangesAsync();
            return true;
        }

        private string GenerateCod()
        {
            Random random = new Random();
            const string caracteres = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Range(0, 8)
                                        .Select(_ => caracteres[random.Next(caracteres.Length)])
                                        .ToArray());
        }

        private async Task HandleLogin(Dictionary<string, string> data, StreamWriter writer)
        {
            if (!data.ContainsKey("username") || !data.ContainsKey("password"))
            {
                await SendJsonAsync(writer, "LOGIN_FAIL", "Datos insuficiente");
                return;
            }

            string username = data["username"];
            string password = data["password"];

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                await SendJsonAsync(writer, "LOGIN_FAIL", "Usuario o contraseña vacíos");
                return;
            }

            bool success = await ValidateLogin(username, password);

            if (success)
            {
                await SendJsonAsync(writer, "LOGIN_SUCCESS", "Login completado correctamente");
            }
            else
            {
                await SendJsonAsync(writer, "LOGIN_SUCCESS", "Usuario o contraseña incorrectos");
            }
        }

        private async Task<bool> ValidateLogin(string username, string password)
        {
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Nombre == username);
            if (user == null) return false;

            return BCryptPasswordHasher.VerifyPassword(password, user.Contrasenia);
        }


        private async Task HandleRegister(Dictionary<string, string> data, StreamWriter writer)
        {
            if (!data.ContainsKey("username") || !data.ContainsKey("password") || !data.ContainsKey("email"))
            {
                await SendJsonAsync(writer, "REGISTER_FAIL", "Datos insuficientes");
                return;
            }

            string username = data["username"];
            string password = data["password"];
            string email = data["email"];

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(email))
            {
                await SendJsonAsync(writer, "REGISTER_FAIL", "Campos vacíos");
                return;
            }

            if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                await SendJsonAsync(writer, "REGISTER_FAIL", "Email inválido");
                return;
            }

            bool success = await RegisterUser(username, password, email);
            if (success)
            {
                await SendJsonAsync(writer, "REGISTER_SUCCESS", "Registro completado correctamente");
            }
            else
            {
                await SendJsonAsync(writer, "REGISTER_FAIL", "Usuario ya existe");
            }
        }

        private async Task<bool> RegisterUser(string username, string password, string email)
        {
            if (_context.Usuarios.Any(u => u.Nombre == username))
                return false; // Usuario ya existe
            if (_context.Usuarios.Any(u => u.Correo == email))
            {
                return false;
            }

            string hasheadaHistorica = BCryptPasswordHasher.HashPassword(password);

            Usuario usu = new Usuario
            {
                Nombre = username,
                Correo = email,
                Contrasenia = hasheadaHistorica,
                Fecha_Creacion = DateTime.Now
            };

            _context.Usuarios.Add(usu);
            await _context.SaveChangesAsync();
            return true;

        }
        private async Task SendJsonAsync(StreamWriter writer, string status, string message, Dictionary<string, string>? data = null)
        {
            var response = new ResponseMessage
            {
                Status = status,
                Message = message,
                Data = data
            };

            string json = JsonSerializer.Serialize(response);
            await writer.WriteLineAsync(json);
        }
    }

    public class Program
    {
        public static async Task Main(string[] args)
        {
            // Assuming you have a valid ChasserContext instance
            using var context = new ChasserContext();

            // Pass the required "context" parameter to the TCPServer constructor
            var server = new TCPServer(context);

            // Start the server on a specific port
            await server.StartAsync(5000);
        }
    }
}
