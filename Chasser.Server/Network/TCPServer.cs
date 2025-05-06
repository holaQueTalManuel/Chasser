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
        private static Queue<(TcpClient client, int userId, string gameCode)> waitingPlayers = new();
        private static Dictionary<string, GameSession> activeGames = new();
        private static Dictionary<TcpClient, string> clientToGameMap = new();
        string gameCod = "";

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
                        case "JOIN_GAME":
                            await JoinGame(msg.Data, writer, client);
                            break;
                        case "LOGOUT":
                            await HandleLogOut(msg.Data, writer);
                            break;
                        
                        default:
                            Console.WriteLine($"Comando no reconocido: {msg.Command}");
                            break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error con cliente: {ex}");
            }
            finally
            {
                // Si estaba esperando, quitarlo de la cola
                var waitingPlayer = waitingPlayers.FirstOrDefault(x => x.client == client);
                if (waitingPlayer.client != null)
                {
                    var newQueue = new Queue<(TcpClient, int, string)>(waitingPlayers.Where(x => x.client != client));
                    waitingPlayers = newQueue;
                }

                // Si estaba en una partida, notificar al oponente
                if (clientToGameMap.TryGetValue(client, out var gameCode))
                {
                    if (activeGames.TryGetValue(gameCode, out var session))
                    {
                        var opponent = session.GetOpponent(client);
                        if (opponent != null && opponent.Connected)
                        {
                            await NotifyOpponentDisconnection(opponent);
                        }
                        activeGames.Remove(gameCode);
                    }
                    clientToGameMap.Remove(client);
                }

                client.Dispose();
            }
        }

        private async Task NotifyOpponentDisconnection(TcpClient opponent)
        {
            try
            {
                using var stream = opponent.GetStream();
                using var writer = new StreamWriter(stream) { AutoFlush = true };
                await SendJsonAsync(writer, "OPPONENT_DISCONNECTED", "Tu oponente se ha desconectado");
            }
            catch
            {
                // Silenciar errores de notificación
            }
        }

        private async Task JoinGame(Dictionary<string, string> data, StreamWriter writer, TcpClient client)
        {
            Console.WriteLine("Procesando JOIN_GAME...");

            try
            {
                // Validaciones básicas
                if (!data.TryGetValue("token", out var token) || string.IsNullOrWhiteSpace(token))
                {
                    await SendJsonAsync(writer, "JOIN_GAME_FAIL", "Token requerido");
                    return;
                }

                var userId = await GetUserFromTokenAsync(token);
                if (userId == null)
                {
                    await SendJsonAsync(writer, "JOIN_GAME_FAIL", "Token inválido");
                    return;
                }

                if (!data.TryGetValue("codigo", out var gameCode) || string.IsNullOrWhiteSpace(gameCode))
                {
                    await SendJsonAsync(writer, "JOIN_GAME_FAIL", "Código de partida necesario");
                    return;
                }

                // Buscar partida en la base de datos
                var partida = await _context.Partidas.FirstOrDefaultAsync(p => p.Codigo == gameCode);
                if (partida == null)
                {
                    await SendJsonAsync(writer, "JOIN_GAME_FAIL", "Partida no encontrada");
                    return;
                }

                // Verificar si la partida ya está llena
                if (partida.Jugador1Id != null && partida.Jugador2Id != null)
                {
                    await SendJsonAsync(writer, "JOIN_GAME_FAIL", "La partida ya está completa");
                    return;
                }

                // Buscar si hay un jugador esperando esta partida específica
                var waitingPlayer = FindWaitingPlayerForGame(gameCode, userId.Value);

                if (waitingPlayer == null)
                {
                    await SendJsonAsync(writer, "JOIN_GAME_FAIL", "No hay jugador esperando en esta partida");
                    return;
                }

                // Actualizar base de datos
                if (partida.Jugador1Id == null)
                {
                    partida.Jugador1Id = userId.Value;
                }
                else
                {
                    partida.Jugador2Id = userId.Value;
                }

                _context.Partidas_Jugadores.Add(new Partida_Jugador
                {
                    PartidaId = partida.Id,
                    UsuarioId = userId.Value
                });
                await _context.SaveChangesAsync();

                // Iniciar sesión de juego
                await StartGameSession(waitingPlayer.Value.client, client, gameCode,
                                     waitingPlayer.Value.userId, userId.Value, writer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en JoinGame: {ex}");
                await SendJsonAsync(writer, "JOIN_GAME_FAIL", "Error interno del servidor");
            }
        }


        private (TcpClient client, int userId)? FindWaitingPlayerForGame(string gameCode, int currentUserId)
        {
            // Hacer una copia de la cola actual para iterar
            var tempQueue = new Queue<(TcpClient client, int userId, string gameCode)>(waitingPlayers);

            while (tempQueue.Count > 0)
            {
                var item = tempQueue.Dequeue();

                // Verificar si cumple las condiciones
                if (item.gameCode == gameCode &&
                    item.userId != currentUserId &&
                    item.client.Connected)
                {
                    // Crear nueva cola sin este jugador
                    waitingPlayers = new Queue<(TcpClient, int, string)>(
                        waitingPlayers.Where(x => x.client != item.client ||
                                                x.userId != item.userId ||
                                                x.gameCode != item.gameCode));

                    return (item.client, item.userId);
                }
            }
            return null;
        }


        private async Task StartGameSession(TcpClient player1, TcpClient player2, string gameCode,
                                  int player1Id, int player2Id, StreamWriter initiatorWriter)
        {
            try
            {
                // Crear la sesión de juego
                var gameSession = new GameSession(player1, player2, gameCode, player1Id, player2Id);

                // Registrar en las estructuras de control
                activeGames.Add(gameCode, gameSession);
                clientToGameMap.Add(player1, gameCode);
                clientToGameMap.Add(player2, gameCode);

                // Obtener nombres de los jugadores
                var player1Name = (await _context.Usuarios.FindAsync(player1Id))?.Nombre;
                var player2Name = (await _context.Usuarios.FindAsync(player2Id))?.Nombre;

                // Notificar al jugador 1 (creador)
                await SendJsonAsync(initiatorWriter, "GAME_STARTED", "Partida iniciada",
                    new Dictionary<string, string> {
                { "codigo", gameCode },
                { "oponente", player2Name },
                { "color", "blanco" }
                    });

                // Notificar al jugador 2 (que se unió)
                using var player2Stream = player2.GetStream();
                using var player2Writer = new StreamWriter(player2Stream) { AutoFlush = true };

                await SendJsonAsync(player2Writer, "GAME_STARTED", "Partida iniciada",
                    new Dictionary<string, string> {
                { "codigo", gameCode },
                { "oponente", player1Name },
                { "color", "negro" }
                    });

                Console.WriteLine($"Partida {gameCode} iniciada entre {player1Name} y {player2Name}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al iniciar sesión: {ex}");

                // Limpiar en caso de error
                activeGames.Remove(gameCode);
                clientToGameMap.Remove(player1);
                clientToGameMap.Remove(player2);

                throw;
            }
        }


        private async Task<int?> GetUserFromTokenAsync(string token)
        {
            var usuario = await _context.Sesiones_Usuarios.FirstOrDefaultAsync(u => u.Token == token);
            return usuario.UsuarioId;
        }

        //private async Task HandleValidateToken(Dictionary<string, string> data, StreamWriter writer)
        //{
        //    try
        //    {
        //        // 1. Validar existencia del token
        //        if (!data.TryGetValue("token", out string? token) || string.IsNullOrWhiteSpace(token))
        //        {
        //            await SendJsonAsync(writer, "TOKEN_INVALID", new { error = "Token requerido" });
        //            return;
        //        }

        //        // 2. Validar contra la base de datos
        //        var user = await ValidateToken(token);

        //        // 3. Responder
        //        await SendJsonAsync(writer,
        //            user != null ? "TOKEN_VALID" : "TOKEN_INVALID",
        //            new
        //            {
        //                valid = user != null,
        //                userId = user?.Id,
        //                username = user?.Nombre
        //            });
        //    }
        //    catch (Exception ex)
        //    {
        //        Console.WriteLine($"Error validando token: {ex.Message}");
        //        await SendJsonAsync(writer, "VALIDATION_ERROR", new { error = ex.Message });
        //    }
        //}

        private async Task HandleLogOut(Dictionary<string, string> data, StreamWriter writer)
        {
            Console.WriteLine("Procesando LOGOUT...");
            if (!data.ContainsKey("token"))
            {
                Console.WriteLine("Falta el token.");
                await SendJsonAsync(writer, "LOGOUT_FAIL", "Token necesario");
                return;
            }
            string token = data["token"];
            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("Token vacío.");
                await SendJsonAsync(writer, "LOGOUT_FAIL", "Token vacío");
                return;
            }
            // Llamar al método ValidateToken para comprobar el token
            Usuario user = await ValidateToken(token);
            // Si el token no es válido o ha expirado, se termina la operación
            if (user == null)
            {
                await SendJsonAsync(writer, "LOGOUT_FAIL", "Token inválido o expirado");
                return;
            }
            // Continuar con la lógica para cerrar sesión
            Console.WriteLine($"Usuario validado: {user.Nombre}");
            // Eliminar la sesión del usuario
            var session = await _context.Sesiones_Usuarios.FirstOrDefaultAsync(s => s.UsuarioId == user.Id);
            if (session != null)
            {
                _context.Sesiones_Usuarios.Remove(session);
                await _context.SaveChangesAsync();
                Console.WriteLine("Sesión eliminada correctamente.");
                await SendJsonAsync(writer, "LOGOUT_SUCCESS", "Sesión cerrada correctamente");
            }
        }

        private async Task GenerateGame(Dictionary<string, string> data, StreamWriter writer, TcpClient client)
        {
            Console.WriteLine("Procesando START_GAME...");

            try
            {
                // Validación del token
                if (!data.ContainsKey("token"))
                {
                    await SendJsonAsync(writer, "START_GAME_FAIL", "Token necesario");
                    return;
                }

                string token = data["token"];
                Usuario user = await ValidateToken(token);

                if (user == null)
                {
                    await SendJsonAsync(writer, "START_GAME_FAIL", "Token inválido o expirado");
                    return;
                }

                // Generar código único para la partida
                string gameCode = GenerateCod();

                // Verificar unicidad del código
                if (_context.Partidas.Any(p => p.Codigo == gameCode))
                {
                    await SendJsonAsync(writer, "START_GAME_FAIL", "Código duplicado, intenta nuevamente");
                    return;
                }

                // Crear registro en la base de datos
                var newGame = new Partida
                {
                    Codigo = gameCode,
                    Jugador1Id = user.Id,
                    Fecha_Creacion = DateTime.UtcNow,
                    Duracion = TimeSpan.Zero,
                    Ganador = ""
                };

                _context.Partidas.Add(newGame);
                await _context.SaveChangesAsync();

                // Registrar también en Partida_Jugador
                _context.Partidas_Jugadores.Add(new Partida_Jugador
                {
                    PartidaId = newGame.Id,
                    UsuarioId = user.Id
                });
                await _context.SaveChangesAsync();

                // Buscar oponente
                var opponent = FindOpponent(client, user.Id, gameCode);

                if (opponent == null)
                {
                    // No hay oponente disponible, poner en cola de espera
                    waitingPlayers.Enqueue((client, user.Id, gameCode));
                    await SendJsonAsync(writer, "START_GAME_WAITING", "Esperando oponente...",
                        new Dictionary<string, string> { { "codigo", gameCode } });
                }
                else
                {
                    // Oponente encontrado, iniciar partida
                    await StartGameSession(client, opponent.Value.client, gameCode, user.Id, opponent.Value.userId, writer);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en GenerateGame: {ex}");
                await SendJsonAsync(writer, "START_GAME_FAIL", "Error interno del servidor");
            }
        }

        private (TcpClient client, int userId)? FindOpponent(TcpClient currentClient, int currentUserId, string gameCode)
        {
            // Limpiar clientes desconectados primero
            CleanDisconnectedPlayers();

            // Buscar oponente válido
            foreach (var candidate in waitingPlayers.ToArray()) // Usar ToArray para evitar modificación durante enumeración
            {
                if (candidate.client.Connected && candidate.userId != currentUserId)
                {
                    // Remover el jugador encontrado de la cola
                    waitingPlayers = new Queue<(TcpClient, int, string)>(
                        waitingPlayers.Where(x => x.client != candidate.client ||
                                                x.userId != candidate.userId));

                    // Devolver solo los dos valores necesarios
                    return (candidate.client, candidate.userId);
                }
            }
            return null;
        }

        private void CleanDisconnectedPlayers()
        {
            // Crear nueva cola solo con jugadores conectados
            var connectedPlayers = waitingPlayers.Where(x => x.client.Connected).ToList();
            waitingPlayers = new Queue<(TcpClient, int, string)>(connectedPlayers);
        }




        private async Task<bool> CreateGame(StreamWriter writer, int id)
        {
            Console.WriteLine("Creando partida...");

            gameCod = GenerateCod();
            Console.WriteLine($"Código generado para partida: {gameCod}");

            if (_context.Partidas.Any(p => p.Codigo == gameCod))
            {
                Console.WriteLine("Código duplicado, abortando creación.");
                await SendJsonAsync(writer, "CREATE_GAME_FAIL", "Se ha generado un mismo código, vuelve a intentarlo");
                return false;
            }

            var jugador1 = await _context.Usuarios.FindAsync(id);

            if (jugador1 == null)
            {
                Console.WriteLine("El usuario no existe.");
                await SendJsonAsync(writer, "CREATE_GAME_FAIL", "El usuario no existe");
                return false;
            }

            var newGame = new Partida
            {
                Ganador = "",
                Codigo = gameCod,
                Jugador1Id = jugador1.Id,
                Duracion = TimeSpan.Zero,
                Fecha_Creacion = DateTime.UtcNow,
            };

            _context.Partidas.Add(newGame);
            await _context.SaveChangesAsync();
            Console.WriteLine("Partida creada y guardada en base de datos.");

            var newPartidaJugador = new Partida_Jugador
            {
                PartidaId = newGame.Id,
                UsuarioId = id
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
