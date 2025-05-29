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
using Serilog;
using Microsoft.IdentityModel.Tokens;
using Microsoft.Extensions.Configuration;

namespace Chasser.Logic.Network
{
    class TCPServer
    {
        private static TcpListener listener;
        private readonly ChasserContext _context;
        public event Action<string> ClientConnected;
        public event Action<string, string> MessageReceived;
        private AIPlayer _aiPlayer = new AIPlayer(Player.Black);
        private Dictionary<TcpClient, GameState> activeAIGames = new();
        private readonly ILogger _logger;
        private Usuario _currentUser;

        public TCPServer(ChasserContext context, ILogger logger)
        {
            _context = context;
            _logger = logger.ForContext<TCPServer>();
        }

        public async Task StartAsync(int port)
        {
            try
            {
                listener = new TcpListener(IPAddress.Any, port);
                listener.Start();
                _logger.Information($"Servidor iniciado en el puerto {port}");

                while (true)
                {
                    TcpClient client = await listener.AcceptTcpClientAsync();
                    string ip = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();
                    _logger.Information($"Cliente conectado desde {ip}");
                    ClientConnected?.Invoke(ip);
                    _ = HandleClientAsync(client);
                }
            }
            catch (Exception ex)
            {
                _logger.Fatal(ex, "Error crítico en el servidor");
                throw;
            }
        }

        private async Task HandleClientAsync(TcpClient client)
        {
            NetworkStream stream = null;
            StreamReader reader = null;
            StreamWriter writer = null;
            string clientIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

            try
            {
                _logger.Debug("Iniciando HandleClientAsync para {ClientIp}", clientIp);
                stream = client.GetStream();
                reader = new StreamReader(stream);
                writer = new StreamWriter(stream) { AutoFlush = true };

                activeAIGames.TryGetValue(client, out var gameState);

                while (client.Connected)
                {
                    _logger.Debug("Esperando datos del cliente {ClientIp}", clientIp);

                    if (!stream.CanRead || (client.Client.Poll(0, SelectMode.SelectRead) && client.Client.Available == 0))
                    {
                        _logger.Debug("Conexión cerrada por el cliente {ClientIp}", clientIp);
                        break;
                    }

                    string message = await reader.ReadLineAsync();
                    _logger.Debug("Mensaje recibido de {ClientIp}: {Message}", clientIp, message);

                    if (message == null)
                    {
                        _logger.Debug("Cliente {ClientIp} desconectado (mensaje nulo)", clientIp);
                        break;
                    }

                    MessageReceived?.Invoke(clientIp, message);

                    var msg = JsonSerializer.Deserialize<RequestMessage>(message);
                    if (msg == null)
                    {
                        _logger.Warning("Mensaje inválido de {ClientIp}", clientIp);
                        continue;
                    }

                    _logger.Information("Comando recibido de {ClientIp}: {Command}", clientIp, msg.Command);

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
                            activeAIGames.TryGetValue(client, out gameState);
                            break;
                        case "RESTART_REQUEST":
                            await HandleRestart(msg.Data, writer, client);
                            activeAIGames.TryGetValue(client, out gameState);
                            break;
                        case "RECOVERY_PASSWORD":
                            await HandleRecoverPassword(msg.Data, writer);
                            break;
                        case "GAME_ACTION_MOVE":
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
                        case "GET_RANKING":
                            await HandleRanking(msg.Data, writer);
                            break;
                        case "DELETE_ACCOUNT":
                            await HandleDeleteAccount(msg.Data, writer);
                            break;
                        default:
                            _logger.Warning("Comando no reconocido de {ClientIp}: {Command}", clientIp, msg.Command);
                            break;
                    }
                }
            }
            catch (IOException ex) when (ex.InnerException is SocketException)
            {
                _logger.Warning(ex, "Error de conexión con {ClientIp}", clientIp);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error manejando cliente {ClientIp}", clientIp);
            }
            finally
            {
                _logger.Debug("Liberando recursos del cliente {ClientIp}", clientIp);
                try
                {
                    activeAIGames.Remove(client);
                    writer?.Dispose();
                    reader?.Dispose();
                    stream?.Dispose();

                    if (client.Connected)
                        client.Close();

                    client.Dispose();
                    _logger.Information("Cliente {ClientIp} desconectado", clientIp);
                }
                catch (Exception ex)
                {
                    _logger.Error(ex, "Error liberando recursos del cliente {ClientIp}", clientIp);
                }
            }
        }

        private async Task HandleDeleteAccount(Dictionary<string, string> data, StreamWriter writer)
        {
            _logger.Information("Procesando DELETE_ACCOUNT...");

            // Validación del token
            if (!data.ContainsKey("token"))
            {
                _logger.Warning("Falta token en DELETE_ACCOUNT");
                await SendJsonAsync(writer, "DELETE_ACCOUNT_FAIL", "Token necesario");
                return;
            }

            string token = data["token"];
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.Warning("Token vacío en DELETE_ACCOUNT");
                await SendJsonAsync(writer, "DELETE_ACCOUNT_FAIL", "Token vacío");
                return;
            }

            try
            {
                using var transaction = await _context.Database.BeginTransactionAsync();

                // 1. Buscar sesión y usuario
                var session = await _context.Sesiones_Usuarios
                    .Include(s => s.Usuario)
                    .FirstOrDefaultAsync(u => u.Token == token);

                if (session?.Usuario == null)
                {
                    _logger.Warning("Token no válido o usuario no encontrado");
                    await SendJsonAsync(writer, "DELETE_ACCOUNT_FAIL", "Credenciales inválidas");
                    return;
                }

                var user = session.Usuario;

                // 2. Eliminar registros relacionados (en orden inverso a las dependencias)
                // - Sesiones del usuario
                var userSessions = await _context.Sesiones_Usuarios
                    .Where(s => s.UsuarioId == user.Id)
                    .ToListAsync();
                _context.Sesiones_Usuarios.RemoveRange(userSessions);

                // - Relación Partidas_Jugadores
                var playerGames = await _context.Partidas_Jugadores
                    .Where(pj => pj.UsuarioId == user.Id)
                    .ToListAsync();
                _context.Partidas_Jugadores.RemoveRange(playerGames);

                // - Partidas donde es jugador1 (si aplica)
                var gamesAsPlayer1 = await _context.Partidas
                    .Where(p => p.Jugador1Id == user.Id)
                    .ToListAsync();
                _context.Partidas.RemoveRange(gamesAsPlayer1);

                // 3. Finalmente eliminar el usuario
                _context.Usuarios.Remove(user);

                // 4. Guardar todos los cambios
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                _logger.Information($"Usuario eliminado: ID {user.Id}");
                await SendJsonAsync(writer, "DELETE_ACCOUNT_SUCCESS", "Cuenta eliminada correctamente");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error al eliminar usuario");
                await SendJsonAsync(writer, "DELETE_ACCOUNT_FAIL", "Error interno del servidor");
            }
        }

        private async Task HandleRanking(Dictionary<string, string> data, StreamWriter writer)
        {
            _logger.Information("Procesando GET_RANKING...");

            // Validación del token
            if (!data.ContainsKey("token"))
            {
                _logger.Warning("Falta token en GET_RANKING");
                await SendJsonAsync(writer, "RANKING_FAIL", "Token necesario");
                return;
            }

            string token = data["token"];
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.Warning("Token vacío en GET_RANKING");
                await SendJsonAsync(writer, "RANKING_FAIL", "Token vacío");
                return;
            }

            Usuario user = await ValidateToken(token);
            if (user == null)
            {
                _logger.Warning("Token inválido en GET_RANKING");
                await SendJsonAsync(writer, "RANKING_FAIL", "Token inválido o expirado");
                return;
            }

            _logger.Information("Usuario validado para ranking: {UserId}", user.Id);

            try
            {
                // Obtener top 10 jugadores ordenados por partidas ganadas
                var topPlayers = await _context.Usuarios
                    .OrderByDescending(u => u.Partidas_Ganadas)
                    .Take(10)
                    .Select(u => new
                    {
                        u.Id,
                        u.Nombre    ,
                        u.Partidas_Ganadas,
                        u.Partidas_Jugadas
                    })
                    .ToListAsync();

                // Preparar datos del ranking
                var rankingData = new List<Dictionary<string, string>>();
                int position = 1;

                foreach (var player in topPlayers)
                {
                    string winRate = player.Partidas_Jugadas > 0
                        ? ((player.Partidas_Ganadas * 100.0) / player.Partidas_Jugadas).ToString()
                        : "0.0";

                    rankingData.Add(new Dictionary<string, string>
                {
                    { "position", position.ToString() },
                    { "username", player.Nombre },
                    { "wins", player.Partidas_Ganadas.ToString() },
                    { "games_played", player.Partidas_Jugadas.ToString() },
                    { "win_rate", winRate }
                });

                    position++;
                }

                // Enviar respuesta exitosa
                await SendJsonObjectAsync(writer, "RANKING_SUCCESS", "Ranking obtenido", new Dictionary<string, JsonElement>
                {
                    { "ranking", ToJsonElement(rankingData) },
                    { "current_user_position", ToJsonElement(await GetUserPosition(user.Id)) }
                });

                _logger.Information("Ranking enviado exitosamente");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error al procesar GET_RANKING");
                await SendJsonAsync(writer, "RANKING_FAIL", "Error interno al obtener ranking");
            }
        }

        // Método auxiliar para obtener la posición global del usuario actual
        private async Task<int> GetUserPosition(int userId)
        {
            var userWins = await _context.Usuarios
                .Where(u => u.Id == userId)
                .Select(u => u.Partidas_Ganadas)
                .FirstOrDefaultAsync();

            return await _context.Usuarios
                .CountAsync(u => u.Partidas_Ganadas > userWins) + 1;
        }

        private async Task HandleRecoverPassword(Dictionary<string, string> data, StreamWriter writer)
        {
            _logger.Information("Procesando RECOVERY_PASSWORD...");

            if (!data.ContainsKey("email"))
            {
                _logger.Warning("Falta el email en RECOVERY_PASSWORD");
                await SendJsonAsync(writer, "RECOVERY_FAIL", "Email necesario");
                return;
            }

            string email = data["email"];
            if (string.IsNullOrWhiteSpace(email))
            {
                _logger.Warning("Email vacío en RECOVERY_PASSWORD");
                await SendJsonAsync(writer, "RECOVERY_FAIL", "Email vacío");
                return;
            }

            string newPassword = data["new_password"];
            if (string.IsNullOrWhiteSpace(newPassword))
            {
                _logger.Warning("Nueva contraseña vacía en RECOVERY_PASSWORD");
                await SendJsonAsync(writer, "RECOVERY_FAIL", "Nueva contraseña vacia");
                return;
            }

            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Correo == email);

            if (user == null)
            {
                _logger.Warning("Intento de recuperación para email no existente: {Email}", email);
                await SendJsonAsync(writer, "RECOVERY_FAIL", "NO EXISTE EL USUARIO");
                return;
            }

            try
            {
                user.Contrasenia = BCryptPasswordHasher.HashPassword(newPassword);
                int rowsAffected = await _context.SaveChangesAsync();

                if (rowsAffected > 0)
                {
                    _logger.Information("Contraseña actualizada para usuario {UserId}", user.Id);
                    await SendJsonAsync(writer, "RECOVERY_SUCCESS", "Contraseña actualizada correctamente");
                }
                else
                {
                    _logger.Warning("No se realizaron cambios al actualizar contraseña para {Email}", email);
                    await SendJsonAsync(writer, "RECOVERY_FAIL", "No se realizaron cambios");
                }
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error al actualizar contraseña para {Email}", email);
                await SendJsonAsync(writer, "RECOVERY_FAIL", "Error interno al guardar");
            }
        }

        private async Task HandleExitGame(StreamWriter writer, TcpClient client)
        {
            _logger.Information("Procesando EXIT_GAME...");

            activeAIGames.Remove(client);
            _logger.Information("Partida eliminada del diccionario activeAIGames");
            await SendJsonAsync(writer, "EXIT_GAME_SUCCESS", "Partida cerrada");
        }

        private async Task HandleRestart(Dictionary<string, string> data, StreamWriter writer, TcpClient client)
        {
            _logger.Information("Procesando RESTART_REQUEST...");

            if (!data.ContainsKey("token"))
            {
                _logger.Warning("Falta el token en RESTART_REQUEST");
                await SendJsonAsync(writer, "RESTART_FAIL", "Token necesario");
                return;
            }

            string token = data["token"];
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.Warning("Token vacío en RESTART_REQUEST");
                await SendJsonAsync(writer, "RESTART_FAIL", "Token vacío");
                return;
            }

            Usuario user = await ValidateToken(token);
            if (user == null)
            {
                _logger.Warning("Token inválido en RESTART_REQUEST");
                await SendJsonAsync(writer, "RESTART_FAIL", "Token inválido");
                return;
            }

            _logger.Information($"Usuario validado para reinicio: {user.Nombre}");
            await SendJsonAsync(writer, "RESTART_ACCEPTED", "La partida ha sido reiniciada.");
            activeAIGames[client] = new GameState(Player.White, Board.Initialize());
            _logger.Information("Nuevo estado de juego creado para {UserId}", user.Id);
        }

        private async Task ProcessPlayerMove(Dictionary<string, string> moveData, GameState gameState, StreamWriter writer, StreamReader reader, TcpClient client)
        {
            string clientIp = ((IPEndPoint)client.Client.RemoteEndPoint).Address.ToString();

            try
            {
                _logger.Debug("Procesando movimiento para {ClientIp}", clientIp);

                if (!writer.BaseStream.CanWrite)
                {
                    _logger.Warning("Stream no disponible para escritura para {ClientIp}", clientIp);
                    return;
                }

                if (!moveData.TryGetValue("fromRow", out var fromRow) ||
                    !moveData.TryGetValue("fromCol", out var fromCol) ||
                    !moveData.TryGetValue("toRow", out var toRow) ||
                    !moveData.TryGetValue("toCol", out var toCol))
                {
                    _logger.Warning("Datos de movimiento incompletos de {ClientIp}", clientIp);
                    await SendJsonAsync(writer, "MOVE_ERROR", "Datos de movimiento incompletos");
                    return;
                }

                var fromPos = new Position(int.Parse(fromRow), int.Parse(fromCol));
                var toPos = new Position(int.Parse(toRow), int.Parse(toCol));
                _logger.Debug("Movimiento recibido de {ClientIp}: {FromPos} → {ToPos}", clientIp, fromPos, toPos);

                var move = new NormalMove(fromPos, toPos);
                var result = gameState.ExecuteMove(move);
                _logger.Debug("Resultado del movimiento: {IsValid}", result.IsValid);

                if (!result.IsValid)
                {
                    _logger.Warning("Movimiento inválido de {ClientIp}: {ErrorMessage}", clientIp, result.ErrorMessage);
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
                    await UpdateDatabaseGameOver(_currentUser, result.Winner.ToString(), gameState.CurrentPlayer.Opponent().ToString());
                    activeAIGames.Remove(client);
                    _logger.Information("Partida terminada para {ClientIp}. Ganador: {Winner}", clientIp, result.Winner);
                    return;
                }

                await SendJsonAsync(writer, "MOVE_ACCEPTED", "Movimiento aceptado", responseData);
                _logger.Debug("Movimiento aceptado enviado a {ClientIp}", clientIp);

                var ackMessage = await ReadAckMessage(reader);
                _logger.Debug("ACK recibido de {ClientIp}: {Command}", clientIp, ackMessage?.Command);

                if (ackMessage?.Command != "MOVE_PROCESSED")
                {
                    _logger.Warning("ACK no recibido correctamente de {ClientIp}", clientIp);
                    return;
                }
                await Task.Delay(1000);
                await MakeAIMove(gameState, writer);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error procesando movimiento de {ClientIp}", clientIp);
                await SendJsonAsync(writer, "MOVE_ERROR", "Error interno del servidor");
            }
        }

        private async Task<RequestMessage?> ReadAckMessage(StreamReader reader)
        {
            try
            {
                string message = await reader.ReadLineAsync();
                return JsonSerializer.Deserialize<RequestMessage>(message);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error leyendo mensaje ACK");
                return null;
            }
        }

        private async Task MakeAIMove(GameState gameState, StreamWriter writer)
        {
            try
            {
                _logger.Debug("Generando movimiento de IA...");
                var aiMove = _aiPlayer.GenerateMove(gameState);

                if (aiMove == null)
                {
                    _logger.Warning("La IA no pudo generar un movimiento válido");
                    return;
                }

                var result = gameState.ExecuteMove(aiMove);
                _logger.Debug("Movimiento IA ejecutado. Válido: {IsValid}", result.IsValid);

                if (!result.IsValid)
                {
                    _logger.Warning("Movimiento de IA inválido: {ErrorMessage}", result.ErrorMessage);
                    return;
                }

                if (result.GameOver)
                {
                    _logger.Information("Juego terminado por IA. Ganador: {Winner}", result.Winner);
                    await HandleGameOver(writer, result, aiMove);
                    await UpdateDatabaseGameOver(_currentUser, result.Winner.ToString(), gameState.CurrentPlayer.Opponent().ToString());
                    return;
                }

                await SendAIMoveToClient(writer, aiMove);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error en MakeAIMove");
            }
        }

        private async Task HandleGameOver(StreamWriter writer, MoveResult result, Move aiMove)
        {
            _logger.Information("GAME_OVER. Ganador: {Winner}", result.Winner);

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

            _logger.Debug("Movimiento de IA enviado al cliente");
        }

        private async Task SaveGameToDatabase(int userId, string gameCode)
        {
            try
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

                _logger.Information("Partida guardada en BD. ID: {GameId}, Código: {GameCode}", newGame.Id, gameCode);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error guardando partida en BD");
            }
        }

        private async Task UpdateDatabaseGameOver(Usuario user, string winner, string playerColor)
        {
            try
            {
                if (user == null)
                {
                    _logger.Warning("Intento de actualizar estadísticas para usuario null");
                    return;
                }

                user.Partidas_Ganadas ??= 0;

                _logger.Debug("Actualizando estadísticas para {UserId}. Ganador: {Winner}", user.Id, winner);

                if (winner == "White")
                {
                    user.Partidas_Ganadas++;
                    user.Racha_Victorias++;
                    _logger.Information("Usuario {UserId} ganó la partida", user.Id);
                }
                else
                {
                    user.Racha_Victorias = 0;
                    _logger.Information("Usuario {UserId} perdió la partida", user.Id);
                }
                user.Partidas_Jugadas++;
                _context.Usuarios.Update(user);
                await _context.SaveChangesAsync();
                _logger.Debug("Estadísticas actualizadas para {UserId}", user.Id);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error actualizando estadísticas en BD");
            }
        }

        private Move GenerateAIMove(GameState gameState)
        {
            var validMoves = new List<Move>();

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
            _logger.Information("Procesando START_GAME_IA...");

            try
            {
                if (!data.TryGetValue("token", out var token))
                {
                    _logger.Warning("Falta token en START_GAME_IA");
                    await SendJsonAsync(writer, "START_GAME_FAIL", "Token necesario");
                    return;
                }

                Usuario user = await ValidateToken(token);
                if (user == null)
                {
                    _logger.Warning("Token inválido en START_GAME_IA");
                    await SendJsonAsync(writer, "START_GAME_FAIL", "Token inválido");
                    return;
                }

                _currentUser = user;
                var gameState = new GameState(Player.White, Board.Initialize());
                activeAIGames[client] = gameState;

                string gameCode = GenerateCod();
                await SaveGameToDatabase(user.Id, gameCode);

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

                _logger.Information("Partida IA iniciada para {UserId}", user.Id);
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error en StartGameAgainstAI");
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
                _logger.Information("Notificada desconexión a oponente");
            }
            catch (Exception ex)
            {
                _logger.Error(ex, "Error notificando desconexión");
            }
        }

        private async Task<int?> GetUserFromTokenAsync(string token)
        {
            var usuario = await _context.Sesiones_Usuarios.FirstOrDefaultAsync(u => u.Token == token);
            return usuario?.UsuarioId;
        }

        private async Task HandleLogOut(Dictionary<string, string> data, StreamWriter writer)
        {
            _logger.Information("Procesando LOGOUT...");

            if (!data.ContainsKey("token"))
            {
                _logger.Warning("Falta token en LOGOUT");
                await SendJsonAsync(writer, "LOGOUT_FAIL", "Token necesario");
                return;
            }

            string token = data["token"];
            if (string.IsNullOrWhiteSpace(token))
            {
                _logger.Warning("Token vacío en LOGOUT");
                await SendJsonAsync(writer, "LOGOUT_FAIL", "Token vacío");
                return;
            }

            Usuario user = await ValidateToken(token);
            if (user == null)
            {
                _logger.Warning("Token inválido en LOGOUT");
                await SendJsonAsync(writer, "LOGOUT_FAIL", "Token inválido o expirado");
                return;
            }

            var session = await _context.Sesiones_Usuarios.FirstOrDefaultAsync(s => s.UsuarioId == user.Id);
            if (session != null)
            {
                _context.Sesiones_Usuarios.Remove(session);
                await _context.SaveChangesAsync();
                _logger.Information("Sesión cerrada para {UserId}", user.Id);
                await SendJsonAsync(writer, "LOGOUT_SUCCESS", "Sesión cerrada correctamente");
            }
        }

        private string GenerateCod()
        {
            Random random = new();
            const string caracteres = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789abcdefghijklmnopqrstuvwxyz";
            return new string(Enumerable.Range(0, 8).Select(_ => caracteres[random.Next(caracteres.Length)]).ToArray());
        }

        private async Task HandleLogin(Dictionary<string, string> data, StreamWriter writer)
        {
            _logger.Information("Procesando LOGIN...");

            if (!data.ContainsKey("username") || !data.ContainsKey("password"))
            {
                _logger.Warning("Faltan credenciales en LOGIN");
                await SendJsonAsync(writer, "LOGIN_FAIL", "Datos insuficientes");
                return;
            }

            string username = data["username"].Trim();
            string password = data["password"].Trim();

            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            {
                _logger.Warning("Credenciales vacías en LOGIN");
                await SendJsonAsync(writer, "LOGIN_FAIL", "Usuario o contraseña vacíos");
                return;
            }

            _currentUser = await ValidateLogin(username, password);

            if (_currentUser != null)
            {
                _logger.Information("Login correcto para {Username}", username);

                var token = Guid.NewGuid().ToString();
                var sesion = new Sesion_Usuario
                {
                    Token = token,
                    Expiration = DateTime.UtcNow.AddHours(2),
                    UsuarioId = _currentUser.Id
                };

                _context.Sesiones_Usuarios.Add(sesion);
                await _context.SaveChangesAsync();

                var responseData = new Dictionary<string, string>
                {
                    { "token", token },
                    { "user_id", _currentUser.Id.ToString() },
                    { "username", _currentUser.Nombre }
                };

                await SendJsonAsync(writer, "LOGIN_SUCCESS", "Login completado", responseData);
                _logger.Debug("Token generado para {UserId}", _currentUser.Id);
            }
            else
            {
                _logger.Warning("Login fallido para {Username}", username);
                await SendJsonAsync(writer, "LOGIN_FAIL", "Usuario o contraseña incorrectos");
            }
        }

        private async Task<Usuario> ValidateLogin(string username, string password)
        {
            var user = await _context.Usuarios.FirstOrDefaultAsync(u => u.Nombre == username);
            if (user == null)
            {
                _logger.Warning("Usuario no encontrado: {Username}", username);
                return null;
            }

            bool match = BCryptPasswordHasher.VerifyPassword(password, user.Contrasenia.Trim());
            _logger.Debug("Resultado verificación contraseña para {Username}: {Match}", username, match);

            return match ? user : null;
        }

        private async Task HandleRegister(Dictionary<string, string> data, StreamWriter writer)
        {
            _logger.Information("Procesando REGISTER...");

            if (!data.ContainsKey("username") || !data.ContainsKey("password") || !data.ContainsKey("email"))
            {
                _logger.Warning("Faltan datos en REGISTER");
                await SendJsonAsync(writer, "REGISTER_FAIL", "Datos insuficientes");
                return;
            }

            string username = data["username"];
            string password = data["password"];
            string email = data["email"];

            _logger.Debug("Datos registro recibidos - Usuario: {Username}, Email: {Email}", username, email);

            if (string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password) || string.IsNullOrEmpty(email))
            {
                _logger.Warning("Campos vacíos en REGISTER");
                await SendJsonAsync(writer, "REGISTER_FAIL", "Campos vacíos");
                return;
            }

            if (!Regex.IsMatch(email, @"^[^@\s]+@[^@\s]+\.[^@\s]+$"))
            {
                _logger.Warning("Email inválido: {Email}", email);
                await SendJsonAsync(writer, "REGISTER_FAIL", "Email inválido");
                return;
            }

            Usuario user = await RegisterUser(username, password, email);

            if (user != null)
            {
                _logger.Information("Registro exitoso para {Username}", username);

                var token = Guid.NewGuid().ToString();
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
                _logger.Debug("Token generado para nuevo usuario {UserId}", user.Id);
            }
            else
            {
                _logger.Warning("Registro fallido - Usuario ya existe: {Username}", username);
                await SendJsonAsync(writer, "REGISTER_FAIL", "Usuario ya existe");
            }
        }

        private async Task<Usuario> RegisterUser(string username, string password, string email)
        {
            if (_context.Usuarios.Any(u => u.Nombre == username || u.Correo == email))
            {
                _logger.Warning("Usuario o email ya existen: {Username}, {Email}", username, email);
                return null;
            }

            var usuario = new Usuario
            {
                Nombre = username,
                Correo = email,
                Contrasenia = BCryptPasswordHasher.HashPassword(password),
                Fecha_Creacion = DateTime.Now,
                Racha_Victorias = 0,
                Partidas_Ganadas = 0,
                Partidas_Jugadas = 0
            };

            _context.Usuarios.Add(usuario);
            await _context.SaveChangesAsync();
            _logger.Information("Nuevo usuario registrado: {UserId}", usuario.Id);
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
            _logger.Debug("Enviando respuesta: {Response}", json);
            await writer.WriteLineAsync(json);
        }

        private async Task SendJsonObjectAsync(StreamWriter writer, string status, string message, Dictionary<string, JsonElement>? data = null)
        {
            var response = new ResponseMessageObject
            {
                Status = status,
                Message = message,
                Data = data
            };

            string json = JsonSerializer.Serialize(response);
            _logger.Debug("Enviando respuesta: {Response}", json);
            await writer.WriteLineAsync(json);
        }

        public async Task<Usuario> ValidateToken(string token)
        {
            if (string.IsNullOrEmpty(token))
            {
                _logger.Warning("Validación token - Token vacío");
                return null;
            }

            var sesion = await _context.Sesiones_Usuarios
                .Include(s => s.Usuario)
                .FirstOrDefaultAsync(s => s.Token == token && s.Expiration > DateTime.UtcNow);

            if (sesion == null)
            {
                _logger.Warning("Validación token - Token inválido o expirado");
                return null;
            }

            sesion.Expiration = DateTime.UtcNow.AddHours(2);
            await _context.SaveChangesAsync();
            _logger.Debug("Token validado para {UserId}", sesion.Usuario.Id);

            return sesion.Usuario;
        }
        private JsonElement ToJsonElement(object obj)
        {
            var json = JsonSerializer.Serialize(obj);
            using var doc = JsonDocument.Parse(json);
            return doc.RootElement.Clone();
        }
    }

    public class Program
    {
        private const int PORT = 5000;

        public static async Task Main(string[] args)
        {
            var logDirectory = Path.Combine(AppContext.BaseDirectory, "logs");
            Directory.CreateDirectory(logDirectory);

            Log.Logger = new LoggerConfiguration()
                .MinimumLevel.Debug()
                .WriteTo.Console()
                .WriteTo.File(
                    "logs/serverlog-.log",
                    rollingInterval: RollingInterval.Day,         // Un archivo por día
                    retainedFileCountLimit: 7,                     // Máximo 7 archivos
                    outputTemplate: "[{Timestamp:HH:mm:ss} {Level:u3}] {Message:lj}{NewLine}{Exception}"
                )
                .CreateLogger();

            try
            {
                Log.Information("Iniciando servidor...");


                var config = new ConfigurationBuilder()
                .SetBasePath(AppContext.BaseDirectory)
                .AddJsonFile("appsettings.json")
                .Build();

                var connectionString = config.GetConnectionString("ChasserDB");

                var optionsBuilder = new DbContextOptionsBuilder<ChasserContext>();
                optionsBuilder.UseSqlServer(connectionString);

                // Crear el contexto con las opciones
                using var context = new ChasserContext(optionsBuilder.Options);

                var server = new TCPServer(context, Log.Logger);
                await server.StartAsync(PORT);
            }
            catch (Exception ex)
            {
                Log.Fatal(ex, "Error fatal en el servidor");
            }
            finally
            {
                Log.CloseAndFlush();
            }
        }
    }
}