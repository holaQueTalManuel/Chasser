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
using Chasser.Server.Network;
using System.Reflection.PortableExecutable;

namespace Chasser.Logic.Network
{
    class TCPServer
    {
        private static TcpListener listener;
        private readonly ChasserContext _context;
        public event Action<string> ClientConnected;
        public event Action<string, string> MessageReceived;
        private AIPlayer _aiPlayer = new AIPlayer(Player.Black);
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
            NetworkStream stream = null;
            StreamReader reader = null;
            StreamWriter writer = null;

            try
            {
                Console.WriteLine("💬 Iniciando HandleClientAsync");
                stream = client.GetStream();
                reader = new StreamReader(stream);
                writer = new StreamWriter(stream) { AutoFlush = true };
                Console.WriteLine("💬 Stream, reader y writer inicializados");

                activeAIGames.TryGetValue(client, out var gameState);

                while (client.Connected)
                {
                    Console.WriteLine("💬 Esperando datos del cliente...");

                    if (!stream.CanRead || (client.Client.Poll(0, SelectMode.SelectRead) && client.Client.Available == 0))
                    {
                        Console.WriteLine("💬 Conexión cerrada por el cliente (verificado manualmente)");
                        break;
                    }

                    string message = await reader.ReadLineAsync();
                    Console.WriteLine($"💬 Línea leída: {message}");

                    if (message == null)
                    {
                        Console.WriteLine("💬 Cliente desconectado (mensaje nulo)");
                        break;
                    }

                    MessageReceived?.Invoke(((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString(), message);

                    var msg = JsonSerializer.Deserialize<RequestMessage>(message);
                    if (msg == null)
                    {
                        Console.WriteLine("💬 Mensaje deserializado es null.");
                        continue;
                    }

                    Console.WriteLine($"💬 Comando recibido: {msg.Command}");

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
                            Console.WriteLine("💬 Procesando START_GAME_IA");
                            await StartGameAgainstAI(msg.Data, writer, client);

                            // ⚠️ Actualizar gameState si se inicia nueva partida
                            activeAIGames.TryGetValue(client, out gameState);
                            break;
                        case "RESTART_REQUEST":
                            Console.WriteLine("💬 Procesando RESTART_REQUEST");
                            await HandleRestart(msg.Data, writer, client);

                            // ❗ ACTUALIZAMOS gameState después del reinicio
                            activeAIGames.TryGetValue(client, out gameState);
                            break;
                        case "GAME_ACTION_MOVE":
                            Console.WriteLine("💬 Procesando GAME_ACTION_MOVE");
                            if (activeAIGames.TryGetValue(client, out gameState))
                            {
                                await ProcessPlayerMove(msg.Data, gameState, writer, reader, client);
                            }
                            else
                            {
                                await SendJsonAsync(writer, "GAME_ERROR", "No hay partida activa");
                            }
                            break;
                        case "EXIT_GAME":
                            await HandleExitGame(writer, client);
                            break;
                        default:
                            Console.WriteLine($"💬 Comando no reconocido: {msg.Command}");
                            break;
                    }
                }
            }
            catch (IOException ex) when (ex.InnerException is SocketException)
            {
                Console.WriteLine($"💥 Error de conexión: {ex.Message}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Error con cliente: {ex}");
            }
            finally
            {
                Console.WriteLine("💬 Liberando recursos del cliente...");
                try
                {
                    activeAIGames.Remove(client);
                    writer?.Dispose();
                    reader?.Dispose();
                    stream?.Dispose();

                    if (client.Connected)
                        client.Close();

                    client.Dispose();
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"💥 Error liberando recursos: {ex}");
                }

                Console.WriteLine("💬 Cliente desconectado y recursos liberados.");
            }
        }

        private async Task HandleExitGame(StreamWriter writer, TcpClient client)
        {
            Console.WriteLine("Procesando EXIT_GAME...");
            
                activeAIGames.Remove(client);
                Console.WriteLine("Partida eliminada del diccionario activeAIGames.");
                await SendJsonAsync(writer, "EXIT_GAME_SUCCESS", "Partida cerrada");
            
            
                //MODIFICAR MAÑANA MARTES PARA DEJARLO BIEN, SOLUCION TEMPORAL
            
        }

        private async Task HandleRestart(Dictionary<string, string> data, StreamWriter writer, TcpClient client)
        {
            Console.WriteLine("Procesando RESTART_REQUEST...");
            if (!data.ContainsKey("token"))
            {
                Console.WriteLine("Falta el token.");
                await SendJsonAsync(writer, "RESTART_FAIL", "Token necesario");
                return;
            }
            string token = data["token"];
            if (string.IsNullOrWhiteSpace(token))
            {
                Console.WriteLine("Token vacío.");
                await SendJsonAsync(writer, "RESTART_FAIL", "Token vacío");
                return;
            }
            // Llamar al método ValidateToken para comprobar el token
            Usuario user = await ValidateToken(token);
            if (user == null)
            {
                await SendJsonAsync(writer, "RESTART_FAIL", "Token inválido");
                return;
            }
            // Continuar con la lógica para reiniciar la partida
            Console.WriteLine($"Usuario validado: {user.Nombre}");
            await SendJsonAsync(writer, "RESTART_ACCEPTED", "La partida ha sido reiniciada.");
            activeAIGames[client] = new GameState(Player.White, Board.Initialize());
        }

        private async Task ProcessPlayerMove(Dictionary<string, string> moveData, GameState gameState, StreamWriter writer, StreamReader reader, TcpClient client)
        {
            try
            {
                Console.WriteLine("💬 Entrando en ProcessPlayerMove");

                if (!writer.BaseStream.CanWrite)
                {
                    Console.WriteLine("💥 Stream no disponible para escritura");
                    return;
                }

                if (!moveData.TryGetValue("fromRow", out var fromRow) ||
                    !moveData.TryGetValue("fromCol", out var fromCol) ||
                    !moveData.TryGetValue("toRow", out var toRow) ||
                    !moveData.TryGetValue("toCol", out var toCol))
                {
                    Console.WriteLine("💥 Datos de movimiento incompletos");
                    await SendJsonAsync(writer, "MOVE_ERROR", "Datos de movimiento incompletos");
                    return;
                }

                var fromPos = new Position(int.Parse(fromRow), int.Parse(fromCol));
                var toPos = new Position(int.Parse(toRow), int.Parse(toCol));
                Console.WriteLine($"💬 Movimiento del jugador: {fromPos} → {toPos}");

                var move = new NormalMove(fromPos, toPos);
                var result = gameState.ExecuteMove(move);
                Console.WriteLine($"💬 Movimiento ejecutado. Es válido: {result.IsValid}");

                if (!result.IsValid)
                {
                    await SendJsonAsync(writer, "MOVE_ERROR", result.ErrorMessage);
                    return;
                }

                var responseData = new Dictionary<string, string>
                {
                    { "fromPos", fromPos.ToString() },
                    { "toPos", toPos.ToString() },
                    { "nextPlayer", gameState.CurrentPlayer.ToString() }
                };

                if (result.GameOver)
                {
                    responseData.Add("winner", result.Winner?.ToString() ?? "draw");
                    await SendJsonAsync(writer, "GAME_OVER", "La partida ha terminado", responseData);
                    await UpdateDatabaseGameOver(user, result.Winner.ToString(), gameState.CurrentPlayer.Opponent().ToString());
                    activeAIGames.Remove(client);
                    return;
                }

                Console.WriteLine("💬 Enviando MOVE_ACCEPTED");
                await SendJsonAsync(writer, "MOVE_ACCEPTED", "Movimiento aceptado", responseData);

                Console.WriteLine("💬 Esperando confirmación MOVE_PROCESSED...");
                var ackMessage = await ReadAckMessage(reader);
                Console.WriteLine($"💬 Mensaje de ACK recibido: {ackMessage?.Command}");

                if (ackMessage?.Command != "MOVE_PROCESSED")
                {
                    Console.WriteLine("💥 Cliente no confirmó MOVE_PROCESSED");
                    return;
                }

                Console.WriteLine("💬 Ejecutando movimiento de IA...");
                await MakeAIMove(gameState, writer);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Error procesando movimiento: {ex}");
                await SendJsonAsync(writer, "MOVE_ERROR", "Error interno del servidor");
            }
        }

        private async Task<RequestMessage?> ReadAckMessage(StreamReader reader)
        {
            try
            {
                Console.WriteLine("💬 Leyendo ACK del cliente...");
                string message = await reader.ReadLineAsync();
                Console.WriteLine($"💬 Línea ACK recibida: {message}");
                return JsonSerializer.Deserialize<RequestMessage>(message);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Error leyendo ACK del cliente: {ex.Message}");
                return null;
            }
        }

        private async Task MakeAIMove(GameState gameState, StreamWriter writer)
        {
            try
            {
                Console.WriteLine("💬 Generando movimiento de IA...");
                var aiMove = _aiPlayer.GenerateMove(gameState);

                if (aiMove == null)
                {
                    Console.WriteLine("💥 La IA no pudo generar un movimiento válido");
                    return;
                }

                var result = gameState.ExecuteMove(aiMove);
                Console.WriteLine($"💬 Movimiento IA ejecutado. Válido: {result.IsValid}");

                if (!result.IsValid)
                {
                    Console.WriteLine($"💥 Movimiento de IA inválido: {result.ErrorMessage}");
                    return;
                }

                if (result.GameOver)
                {
                    Console.WriteLine("💬 Juego terminado por IA. Enviando GAME_OVER.");
                    await HandleGameOver(writer, result, aiMove);
                    await UpdateDatabaseGameOver(user, result.Winner.ToString(), gameState.CurrentPlayer.Opponent().ToString());
                    return;
                }

                await SendAIMoveToClient(writer, aiMove);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"💥 Error en MakeAIMove: {ex}");
            }
        }

        private async Task HandleGameOver(StreamWriter writer, MoveResult result, Move aiMove)
        {
            Console.WriteLine($"DESDE EL SERVER: GANADOR: {result.Winner?.ToString() ?? "Empate"}");

            await SendJsonAsync(writer, "GAME_OVER", "La partida ha terminado",
                new Dictionary<string, string>
                {
            { "winner", result.Winner?.ToString() ?? "draw" },
            { "fromRow", aiMove.FromPos.Row.ToString() },
            { "fromCol", aiMove.FromPos.Column.ToString() },
            { "toRow", aiMove.ToPos.Row.ToString() },
            { "toCol", aiMove.ToPos.Column.ToString() }
                });
            
        }

        private async Task SendAIMoveToClient(StreamWriter writer, Move aiMove)
        {
            await SendJsonAsync(writer, "AI_MOVE", "Movimiento de IA",
                new Dictionary<string, string>
                {
            { "fromRow", aiMove.FromPos.Row.ToString() },
            { "fromCol", aiMove.FromPos.Column.ToString() },
            { "toRow", aiMove.ToPos.Row.ToString() },
            { "toCol", aiMove.ToPos.Column.ToString() }
                });

            Console.WriteLine("Movimiento de IA enviado. Esperando respuesta del cliente...");
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

        private async Task UpdateDatabaseGameOver(Usuario user, string winner, string playerColor)
        {
            Console.WriteLine("Entrando en UpdateDatabaseGameOver");

            if (user == null)
            {
                Debug.WriteLine("Usuario es null. No se puede actualizar.");
                return;
            }
            user.Partidas_Ganadas ??= 0;

            Console.WriteLine($"Usuario antes de actualizar: {user.Nombre}, Partidas_Ganadas: {user.Partidas_Ganadas}, Racha_Victorias: {user.Racha_Victorias}");
            Console.WriteLine($"Ganador: {winner}");

            if (winner == "White")
            {
                user.Partidas_Ganadas++;
                user.Racha_Victorias++;
                Console.WriteLine("El jugador ha ganado. Incrementando estadísticas.");
            }
            else
            {
                user.Racha_Victorias = 0;
                Console.WriteLine("El jugador ha perdido. Reiniciando racha de victorias.");
            }

            try
            {
                _context.Usuarios.Update(user);
                await _context.SaveChangesAsync();
                Console.WriteLine("Datos guardados correctamente en la base de datos.");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error al guardar en la base de datos: {ex.Message}");
            }

            Console.WriteLine($"Usuario después de actualizar: {user.Nombre}, Partidas_Ganadas: {user.Partidas_Ganadas}, Racha_Victorias: {user.Racha_Victorias}");
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
                        { "oponente", "IA" },
                        { "nombreUsuario", user.Nombre },
                        { "partidasGanadas", user.Partidas_Ganadas.ToString() },
                        { "racha", user.Racha_Victorias.ToString() }
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

            Usuario user = await RegisterUser(username, password, email);

            if (user != null)
            {
                Console.WriteLine("Registro completado correctamente.");
                Console.WriteLine("GENERANDO TOKEN PARA REGISTRO");


                // 1. Generar token único
                var token = Guid.NewGuid().ToString();

                // 2. Crear nueva sesión
                var sesion = new Sesion_Usuario
                {
                    Token = token,
                    Expiration = DateTime.UtcNow.AddHours(2),
                    UsuarioId = user.Id
                };

                _context.Sesiones_Usuarios.Add(sesion);
                await _context.SaveChangesAsync();
                var responseData = new Dictionary<string, string>
                {
                    { "token", token },
                    { "user_id", user.Id.ToString() },
                    { "username", user.Nombre }
                };

                await SendJsonAsync(writer, "REGISTER_SUCCESS", "Registro completado correctamente", responseData);
            }
            else
            {
                Console.WriteLine("Registro fallido: usuario ya existe.");
                await SendJsonAsync(writer, "REGISTER_FAIL", "Usuario ya existe");
            }
        }

        private async Task<Usuario> RegisterUser(string username, string password, string email)
        {
            Console.WriteLine("Intentando registrar usuario en base de datos...");

            if (_context.Usuarios.Any(u => u.Nombre == username || u.Correo == email))
            {
                Console.WriteLine("Usuario o correo ya existente.");
                return null;
            }

            var usuario = new Usuario
            {
                Nombre = username,
                Correo = email,
                Contrasenia = BCryptPasswordHasher.HashPassword(password),
                Fecha_Creacion = DateTime.Now,
                Racha_Victorias = 0,
                Partidas_Ganadas = 0

            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();
            Console.WriteLine("Usuario registrado correctamente en la base de datos.");
            return usuario;
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
