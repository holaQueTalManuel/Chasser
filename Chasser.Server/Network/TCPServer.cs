using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Windows.Controls;
using Chasser.Model;
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

                        string[] parts = message.Split('|');
                        if (parts.Length == 0) continue;

                        string command = parts[0];

                        switch (command)
                        {
                            //manejar caso de registro
                            case "REGISTER":
                                await HandleRegister(parts, writer);
                                break;
                            //manejar caso de login
                            case "LOGIN":
                                await HandleLogin(parts, writer);
                                break;
                            case "START_GAME":
                                await GenerateGame(parts, writer);
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

        private async Task GenerateGame(string[] parts, StreamWriter writer)
        {
            Usuario userId = null;
            if (parts.Length < 1)
            {
                await writer.WriteLineAsync("START_GAME_FAIL|Datos insuficientes");
                return;
            }

            if (File.Exists(userPath))
            {
                string savedUser = File.ReadAllText(userPath);
                userId = _context.Usuarios.FirstOrDefault(u => u.Nombre == savedUser);

                if (userId == null)
                {
                    await writer.WriteLineAsync("START_GAME_FAIL|No se ha encontrado ningun usuario logueado");
                    return;
                }

            }

            int playerId = _context.Usuarios
                .Where(x => x.Id == userId.Id)
                .Select(y => y.Id)
                .FirstOrDefault();

            bool success = await CreateGame(writer, playerId);
            await writer.WriteLineAsync(success ? "START_GAME_SUCESS" : "START_GAME_FAIL|No se ha podido crear la partida");
        }

        private async Task<bool> CreateGame(StreamWriter writer, int id)
        {
            string gameCod = GenerateCod();

            if (_context.Partidas.Any(p => p.Codigo == gameCod))
            {
                await writer.WriteLineAsync("LOGIN_FAIL|Se ha generado un mismo codigo, vuelve a intentarlo");
                return false;
            }
            Partida newGame = new Partida
            {

                Ganador = "",
                Codigo = gameCod,
                Duracion = DateTime.Now,
                Fecha_Creacion = DateTime.Now
            };

            Partida_Jugador newPartidaJugador = null;


            newPartidaJugador = new Partida_Jugador
            {
                PartidaId = newGame.Id,
                Jugador1 = id,
            };

            _context.Partidas.Add(newGame);
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

        private async Task HandleLogin(string[] parts, StreamWriter writer)
        {
            if (parts.Length < 3)
            {
                await writer.WriteLineAsync("LOGIN_FAIL|Datos insuficientes");
                return;
            }

            string username = parts[1].Trim();
            string password = parts[2].Trim();

            bool success = await ValidateLogin(username, password);

            await writer.WriteLineAsync(success ? "LOGIN_SUCCESS" : "LOGIN_FAIL|Usuario o contraseña incorrectos");
        }

        private async Task<bool> ValidateLogin(string username, string password)
        {
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Nombre == username);
            if (user == null) return false;

            return BCryptPasswordHasher.VerifyPassword(password, user.Contrasenia);
        }


        private async Task HandleRegister(string[] parts, StreamWriter writer)
        {
            if (parts.Length < 4)
            {
                await writer.WriteLineAsync("REGISTER_FAIL|Datos insuficientes");
                return;
            }

            string username = parts[1].Trim();
            string password = parts[2].Trim();
            string email = parts[3].Trim();

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(email))
            {
                await writer.WriteLineAsync("REGISTER_FAIL|Campos vacíos");
                return;
            }

            if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                await writer.WriteLineAsync("REGISTER_FAIL|Email inválido");
                return;
            }

            bool success = await RegisterUser(username, password, email);
            await writer.WriteLineAsync(success ? "REGISTER_SUCCESS" : "REGISTER_FAIL|Usuario ya existe");
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
    }
}
