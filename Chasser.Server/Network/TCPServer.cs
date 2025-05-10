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
using Chasser.Common.Logic.Board;
using Chasser.Common.Logic.Moves;

namespace Chasser.Logic.Network
{
    class TCPServer
    {
        private static TcpListener listener;
        private readonly ChasserContext _context;
        public event Action<string> ClientConnected;
        public event Action<string, string> MessageReceived;

        // Diccionario para mantener los juegos activos contra IA
        private Dictionary<TcpClient, GameState> activeAIGames = new();

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

                while (client.Connected && stream.CanRead)
                {
                    string message = await reader.ReadLineAsync();
                    if (message == null)
                    {
                        Console.WriteLine("Cliente desconectado.");
                        break;
                    }

                    Console.WriteLine($"Mensaje recibido: {message}");
                    MessageReceived?.Invoke(((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString(), message);

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
                        case "LOGOUT":
                            await HandleLogOut(msg.Data, writer);
                            break;
                        case "START_GAME_IA":
                            await StartGameAgainstAI(msg.Data, writer, client);
                            break;
                        case "GAME_ACTION_MOVE":
                            if (activeAIGames.TryGetValue(client, out var gameState))
                            {
                                await ProcessPlayerMove(msg.Data, gameState, writer);
                            }
                            else
                            {
                                await SendJsonAsync(writer, "GAME_ERROR", "No hay partida activa");
                            }
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
                activeAIGames.Remove(client);
                client.Dispose();
                Console.WriteLine("Cliente desconectado y recursos liberados.");
            }
        }

        private async Task ProcessPlayerMove(Dictionary<string, string> moveData, GameState gameState, StreamWriter writer)
        {
            try
            {
                // Validación adicional
                if (!moveData.TryGetValue("fromRow", out var fromRow) ||
                    !moveData.TryGetValue("fromCol", out var fromCol) ||
                    !moveData.TryGetValue("toRow", out var toRow) ||
                    !moveData.TryGetValue("toCol", out var toCol))
                {
                    await SendJsonAsync(writer, "MOVE_ERROR", "Datos de movimiento incompletos");
                    return;
                }

                var fromPos = new Position(int.Parse(fromRow), int.Parse(fromCol));
                var toPos = new Position(int.Parse(toRow), int.Parse(toCol));

                // Debugging: Mostrar estado actual
                Console.WriteLine($"Intento de mover de {fromPos} a {toPos}");
                Console.WriteLine($"Pieza en origen: {gameState.Board[fromPos]?.ToString() ?? "vacía"}");
                Console.WriteLine($"Pieza en destino: {gameState.Board[toPos]?.ToString() ?? "vacía"}");
                Console.WriteLine($"Turno actual: {gameState.CurrentPlayer}");

                var move = new NormalMove(fromPos, toPos);
                var result = gameState.ExecuteMove(move);

                if (!result.IsValid)
                {
                    Console.WriteLine($"Movimiento inválido: {result.ErrorMessage}");
                    await SendJsonAsync(writer, "MOVE_ERROR", result.ErrorMessage);
                    return;
                }

                await SendJsonAsync(writer, "MOVE_ACCEPTED", "Movimiento aceptado");

                // Movimiento de IA
                await MakeAIMove(gameState, writer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error procesando movimiento: {ex}");
                await SendJsonAsync(writer, "MOVE_ERROR", "Error interno del servidor");
            }
        }

        private async Task MakeAIMove(GameState gameState, StreamWriter writer)
        {
            var aiMove = GenerateAIMove(gameState);
            if (aiMove != null)
            {
                var result = gameState.ExecuteMove(aiMove);
                if (result.IsValid)
                {
                    await SendJsonAsync(writer, "AI_MOVE", "Movimiento de IA",
                        new Dictionary<string, string>
                        {
                            { "fromRow", aiMove.FromPos.Row.ToString() },
                            { "fromCol", aiMove.FromPos.Column.ToString() },
                            { "toRow", aiMove.ToPos.Row.ToString() },
                            { "toCol", aiMove.ToPos.Column.ToString() }
                        });

                    if (result.GameOver)
                    {
                        await SendJsonAsync(writer, "GAME_OVER", "La partida ha terminado",
                            new Dictionary<string, string>
                            {
                                { "winner", result.Winner?.ToString() ?? "draw" }
                            });
                    }
                }
            }
        }

        private async Task SaveGameToDatabase(int userId, string gameCode)
        {
            var newGame = new Partida
            {
                Codigo = gameCode,
                Jugador1Id = userId,
                Fecha_Creacion = DateTime.UtcNow,
                Duracion = TimeSpan.Zero,
                Ganador = ""
            };

            _context.Partidas.Add(newGame);
            await _context.SaveChangesAsync();

            _context.Partidas_Jugadores.Add(new Partida_Jugador
            {
                PartidaId = newGame.Id,
                UsuarioId = userId
            });
            await _context.SaveChangesAsync();
        }

        private Move GenerateAIMove(GameState gameState)
        {
            var validMoves = new List<Move>();

            // Buscar todos los movimientos válidos para las piezas negras (IA)
            for (int row = 0; row < 7; row++)
            {
                for (int col = 0; col < 7; col++)
                {
                    var pos = new Position(row, col);
                    var piece = gameState.Board[pos];
                    if (piece?.Color == Player.Black)
                    {
                        validMoves.AddRange(gameState.GetLegalMoves(pos));
                    }
                }
            }

            return validMoves.Count > 0 ? validMoves[new Random().Next(validMoves.Count)] : null;
        }

        private async Task StartGameAgainstAI(Dictionary<string, string> data, StreamWriter writer, TcpClient client)
        {
            Console.WriteLine("Procesando START_GAME_IA...");

            try
            {
                // Validar token
                if (!data.TryGetValue("token", out var token))
                {
                    await SendJsonAsync(writer, "START_GAME_FAIL", "Token necesario");
                    return;
                }

                Usuario user = await ValidateToken(token);
                if (user == null)
                {
                    await SendJsonAsync(writer, "START_GAME_FAIL", "Token inválido");
                    return;
                }

                // Crear estado del juego
                var gameState = new GameState(Player.White, Board.Initialize());
                activeAIGames[client] = gameState;

                // Opcional: Guardar en base de datos
                string gameCode = GenerateCod();
                await SaveGameToDatabase(user.Id, gameCode);

                // Responder al cliente
                await SendJsonAsync(writer, "START_GAME_SUCCESS", "Partida contra IA iniciada",
                    new Dictionary<string, string>
                    {
                        { "color", "white" },
                        { "codigo", gameCode },
                        { "oponente", "IA" }
                    });

                Console.WriteLine($"Partida contra IA iniciada para {user.Nombre}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error en StartGameAgainstAI: {ex}");
                await SendJsonAsync(writer, "START_GAME_FAIL", "Error iniciando partida");
                activeAIGames.Remove(client);
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



        


    


        private async Task<int?> GetUserFromTokenAsync(string token)
        {
            var usuario = await _context.Sesiones_Usuarios.FirstOrDefaultAsync(u => u.Token == token);
            return usuario.UsuarioId;
        }

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
        

        private string GenerateCod()
        {
            Random random = new();
            const string caracteres = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz";
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
