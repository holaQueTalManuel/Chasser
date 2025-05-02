using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text.Json;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using Chasser.Common;
using Chasser.Common.Network;
using Chasser.Server.Network;
using Microsoft.EntityFrameworkCore;
using Chasser.Common.Data;
using Chasser.Common.Model;

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

        public TCPServer(ChasserContext context)
        {
            _context = context;
        }

        public async Task StartAsync(int port)
        {
            listener = new TcpListener(IPAddress.Any, port);
            listener.Start();
            Console.WriteLine($"Servidor iniciado en el puerto {port}");

            while (true)
            {
                TcpClient client = await listener.AcceptTcpClientAsync();
                string ip = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                Console.WriteLine($"Cliente conectado desde {ip}");
                ClientConnected?.Invoke(ip);
                _ = HandleClientAsync(client);
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            try
            {
                using var stream = client.GetStream();
                using var reader = new StreamReader(stream);
                using var writer = new StreamWriter(stream) { AutoFlush = true };

                while (client.Connected)
                {
                    string message = await reader.ReadLineAsync();
                    if (message == null)
                    {
                        Console.WriteLine("Mensaje nulo, cerrando conexión.");
                        break;
                    }

                    Console.WriteLine($"Mensaje recibido: {message}");

                    MessageReceived?.Invoke(
                        ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString(),
                        message
                    );

                    var msg = JsonSerializer.Deserialize<RequestMessage>(message);
                    if (msg == null)
                    {
                        Console.WriteLine("Mensaje deserializado es null.");
                        continue;
                    }

                    Console.WriteLine($"Comando recibido: {msg.Command}");

                    switch (msg.Command)
                    {
                        case "REGISTER":
                            await HandleRegister(msg.Data, writer);
                            break;
                        case "LOGIN":
                            await HandleLogin(msg.Data, writer);
                            break;
                        case "START_GAME":
                            await GenerateGame(msg.Data, writer, client);
                            break;
                        default:
                            Console.WriteLine($"Comando no reconocido: {msg.Command}");
                            break;
                    }
                }
            }
            catch (IOException ex)
            {
                Console.WriteLine($"Error de lectura: {ex.Message}");
            }
            catch (SocketException ex)
            {
                Console.WriteLine($"Error de socket: {ex.Message}");
            }
            finally
            {
                client.Dispose();
                Console.WriteLine("Cliente desconectado.");
            }
        }

        private async Task GenerateGame(Dictionary<string, string> data, StreamWriter writer, TcpClient client)
        {
            Console.WriteLine("Procesando START_GAME...");

            try
            {
                // Validar el token recibido
                if (!data.ContainsKey("token"))
                {
                    Console.WriteLine("Falta el token.");
                    await SendJsonAsync(writer, "START_GAME_FAIL", "Token necesario");
                    return;
                }

                string token = data["token"];

                if (string.IsNullOrWhiteSpace(token))
                {
                    Console.WriteLine("Token vacío.");
                    await SendJsonAsync(writer, "START_GAME_FAIL", "Token vacío");
                    return;
                }

                // Llamar al método ValidateToken para comprobar el token
                Usuario user = await ValidateToken(token);

                // Si el token no es válido o ha expirado, se termina la operación
                if (user == null)
                {
                    await SendJsonAsync(writer, "START_GAME_FAIL", "Token inválido o expirado");
                    return;
                }

                // Continuar con la lógica para iniciar el juego
                Console.WriteLine($"Usuario validado: {user.Nombre}");

                bool success = await CreateGame(writer, user.Id);

                if (success)
                {
                    Console.WriteLine("Partida creada. Gestionando emparejamiento...");

                    while (waitingClients.Count > 0 && !waitingClients.Peek().Connected)
                    {
                        waitingClients.Dequeue();
                        Console.WriteLine("Cliente desconectado eliminado de la cola.");
                    }

                    if (waitingClients.Count < 1)
                    {
                        waitingClients.Enqueue(client);
                        Console.WriteLine("Jugador añadido a la cola. Esperando oponente...");
                        await SendJsonAsync(writer, "WAITING_FOR_OPPONENT", "Esperando a un segundo jugador...");
                    }
                    else
                    {
                        TcpClient opponent = waitingClients.Dequeue();
                        Console.WriteLine("Segundo jugador encontrado. Iniciando sesión de juego.");
                        new GameSession(client, opponent);
                        await SendJsonAsync(writer, "START_GAME_SUCCESS", "Partida creada correctamente");
                    }
                }
                else
                {
                    Console.WriteLine("No se pudo crear la partida.");
                    await SendJsonAsync(writer, "START_GAME_FAIL", "No se ha podido crear la partida");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GenerateGame: {ex.Message}");
                await SendJsonAsync(writer, "START_GAME_FAIL", $"Error interno en el servidor: {ex.Message}");
            }
        }



        private async Task<bool> CreateGame(StreamWriter writer, int id)
        {
            Console.WriteLine("Creando partida...");

            string gameCod = GenerateCod();
            Console.WriteLine($"Código generado para partida: {gameCod}");

            if (_context.Partidas.Any(p => p.Codigo == gameCod))
            {
                Console.WriteLine("Código duplicado, abortando creación.");
                await SendJsonAsync(writer, "CREATE_GAME_FAIL", "Se ha generado un mismo código, vuelve a intentarlo");
                return false;
            }

            var newGame = new Partida
            {
                Ganador = "",
                Codigo = gameCod,
                Duracion = DateTime.Now,
                Fecha_Creacion = DateTime.Now
            };

            _context.Partidas.Add(newGame);
            await _context.SaveChangesAsync();
            Console.WriteLine("Partida creada y guardada en base de datos.");

            var newPartidaJugador = new Partida_Jugador
            {
                PartidaId = newGame.Id,
                Jugador1Id = id
            };

            _context.Partidas_Jugadores.Add(newPartidaJugador);
            await _context.SaveChangesAsync();
            Console.WriteLine("Jugador asignado a la partida.");

            return true;
        }

        private string GenerateCod()
        {
            Random random = new();
            const string caracteres = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";
            return new string(Enumerable.Range(0, 8).Select(_ => caracteres[random.Next(caracteres.Length)]).ToArray());
        }

        private Usuario user; // Variable de clase para mantener el usuario autenticado

        private async Task HandleLogin(Dictionary<string, string> data, StreamWriter writer)
        {
            Console.WriteLine("Procesando LOGIN...");

            // Validación de campos (ya lo tienes bien)
            if (!data.ContainsKey("username") || !data.ContainsKey("password"))
            {
                Console.WriteLine("Faltan campos de login.");
                await SendJsonAsync(writer, "LOGIN_FAIL", "Datos insuficientes");
                return;
            }

            string username = data["username"].Trim();
            string password = data["password"].Trim();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                Console.WriteLine("Usuario o contraseña vacíos");
                await SendJsonAsync(writer, "LOGIN_FAIL", "Usuario o contraseña vacíos");
                return;
            }

            // Validar credenciales
            user = await ValidateLogin(username, password);

            if (user != null)
            {
                Console.WriteLine("Login correcto.");

                // 1. Generar token único
                var token = Guid.NewGuid().ToString();

                // 2. Crear nueva sesión
                var sesion = new Sesion_Usuario
                {
                    Token = token,
                    Expiration = DateTime.UtcNow.AddHours(2),
                    UsuarioId = user.Id
                };

                // 3. Guardar en base de datos
                _context.Sesiones_Usuarios.Add(sesion);
                await _context.SaveChangesAsync();

                // 4. Enviar token al cliente junto con la respuesta
                var responseData = new Dictionary<string, string>
                {
                    { "token", token },
                    { "user_id", user.Id.ToString() },
                    { "username", user.Nombre }
                };

                await SendJsonAsync(writer, "LOGIN_SUCCESS", "Login completado", responseData);
            }
            else
            {
                Console.WriteLine("Login fallido.");
                await SendJsonAsync(writer, "LOGIN_FAIL", "Usuario o contraseña incorrectos");
            }
        }

        private async Task<Usuario> ValidateLogin(string username, string password)
        {
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Nombre == username);
            if (user == null)
            {
                Console.WriteLine("Usuario no encontrado en la base de datos.");
                return null;
            }

            Console.WriteLine($"Usuario encontrado. Comparando contraseñas...");
            Console.WriteLine($"Hash guardado: {user.Contrasenia}");

            bool match = BCryptPasswordHasher.VerifyPassword(password, user.Contrasenia.Trim());
            Console.WriteLine($"Resultado de comparación: {match}");

            return match ? user : null;
        }


        private async Task HandleRegister(Dictionary<string, string> data, StreamWriter writer)
        {
            Console.WriteLine("Procesando REGISTER...");

            if (!data.ContainsKey("username") || !data.ContainsKey("password") || !data.ContainsKey("email"))
            {
                Console.WriteLine("Faltan campos para el registro.");
                await SendJsonAsync(writer, "REGISTER_FAIL", "Datos insuficientes");
                return;
            }

            string username = data["username"];
            string password = data["password"];
            string email = data["email"];

            Console.WriteLine($"Datos recibidos -> Usuario: {username}, Email: {email}");

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(email))
            {
                Console.WriteLine("Campos vacíos en el registro.");
                await SendJsonAsync(writer, "REGISTER_FAIL", "Campos vacíos");
                return;
            }

            if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                Console.WriteLine("Email inválido.");
                await SendJsonAsync(writer, "REGISTER_FAIL", "Email inválido");
                return;
            }

            bool success = await RegisterUser(username, password, email);

            if (success)
            {
                Console.WriteLine("Registro completado correctamente.");
                await SendJsonAsync(writer, "REGISTER_SUCCESS", "Registro completado correctamente");
            }
            else
            {
                Console.WriteLine("Registro fallido: usuario ya existe.");
                await SendJsonAsync(writer, "REGISTER_FAIL", "Usuario ya existe");
            }
        }

        private async Task<bool> RegisterUser(string username, string password, string email)
        {
            Console.WriteLine("Intentando registrar usuario en base de datos...");

            if (_context.Usuarios.Any(u => u.Nombre == username || u.Correo == email))
            {
                Console.WriteLine("Usuario o correo ya existente.");
                return false;
            }

            var usuario = new Usuario
            {
                Nombre = username,
                Correo = email,
                Contrasenia = BCryptPasswordHasher.HashPassword(password),
                Fecha_Creacion = DateTime.Now
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();
            Console.WriteLine("Usuario registrado correctamente en la base de datos.");
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
            Console.WriteLine($"Enviando respuesta al cliente: {json}");
            await writer.WriteLineAsync(json);
        }
        public async Task<Usuario> ValidateToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                Console.WriteLine("Token no proporcionado");
                return null;
            }

            // Buscar el token en la base de datos con su expiración
            var sesion = await _context.Sesiones_Usuarios
                .Include(s => s.Usuario) // Carga los datos del usuario
                .FirstOrDefaultAsync(s => s.Token == token && s.Expiration > DateTime.UtcNow);

            if (sesion == null)
            {
                Console.WriteLine("Token inválido o expirado");
                return null;
            }

            // Opcional: Extender la validez del token
            sesion.Expiration = DateTime.UtcNow.AddHours(2);
            await _context.SaveChangesAsync();

            return sesion.Usuario; // Devuelve el usuario asociado al token
        }
    }

    public class Program
    {
        public static async Task Main(string[] args)
        {
            using var context = new ChasserContext();
            var server = new TCPServer(context);
            await server.StartAsync(5000);
        }
    }
}
