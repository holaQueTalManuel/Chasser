using Chasser.Common.Logic;
using Chasser.Common.Logic.Board;
using Chasser.Common.Logic.Moves;
using Chasser.Common.Network;

using System.Net.Sockets;
using System.Text.Json;

public class GameSession
{
    private readonly TcpClient player1;
    private readonly TcpClient player2;
    private readonly string gameCode;
    private readonly GameState gameState;
    private bool isGameOver = false;
    private bool isAgainstAI;

    public GameSession(TcpClient player1, TcpClient player2, string gameCode, int player1Id, int player2Id, bool isAgainstAI = false)
    {
        this.player1 = player1;
        this.player2 = player2;
        this.gameCode = gameCode;

        // Inicializar estado del juego
        gameState = new GameState(Player.White, Board.Initialize());
        gameState.RegisterPlayer(Player.White, player1Id);
        gameState.RegisterPlayer(Player.Black, player2Id);
        this.isAgainstAI = isAgainstAI;

    }

    public TcpClient GetOpponent(TcpClient client)
    {
        if (client == player1) return player2;
        if (client == player2) return player1;
        return null;
    }

    public async Task RunSessionAsync()
    {
        try
        {
            using var stream1 = player1.GetStream();
            var reader1 = new StreamReader(stream1);
            var writer1 = new StreamWriter(stream1) { AutoFlush = true };

            StreamWriter? writer2 = null;
            StreamReader? reader2 = null;

            if (!isAgainstAI)
            {
                var stream2 = player2.GetStream();
                reader2 = new StreamReader(stream2);
                writer2 = new StreamWriter(stream2) { AutoFlush = true };
            }

            Console.WriteLine($"[GameSession {gameCode}] Iniciando partida. Contra IA: {isAgainstAI}");

            await SendStartMessages(writer1, writer2, gameState.Players);

            if (isAgainstAI)
            {
                await HandleHumanVsAI(reader1, writer1);
            }
            else
            {
                var task1 = HandlePlayerMessages(reader1, writer2!, Player.White);
                var task2 = HandlePlayerMessages(reader2!, writer1, Player.Black);
                await Task.WhenAny(task1, task2);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error en sesión {gameCode}: {ex}");
        }
        finally
        {
            isGameOver = true;
            player1.Dispose();
            player2.Dispose();
            Console.WriteLine($"Sesión {gameCode} finalizada");
        }
    }

    private async Task HandleHumanVsAI(StreamReader humanReader, StreamWriter humanWriter)
    {
        try
        {
            while (!isGameOver && humanReader.BaseStream.CanRead)
            {
                var message = await humanReader.ReadLineAsync();
                if (message == null) break;

                var request = JsonSerializer.Deserialize<RequestMessage>(message);
                if (request == null || request.Command != "GAME_ACTION_MOVE") continue;

                Console.WriteLine($"[GameSession {gameCode}] Movimiento del jugador humano recibido.");

                await HandleMoveAsync(request, null, Player.White); // El segundo parámetro no es necesario aquí

                if (!isGameOver)
                {
                    await Task.Delay(500); // pequeña pausa para simular IA "pensando"
                    var aiMove = GenerateAIMove();

                    if (aiMove != null)
                    {
                        var aiMessage = new RequestMessage
                        {
                            Command = "GAME_ACTION_MOVE",
                            Data = new Dictionary<string, string>
                        {
                            { "fromRow", aiMove.FromPos.Row.ToString() },
                            { "fromCol", aiMove.FromPos.Column.ToString() },
                            { "toRow", aiMove.ToPos.Row.ToString() },
                            { "toCol", aiMove.ToPos.Column.ToString() }
                        }
                        };

                        await HandleMoveAsync(aiMessage, humanWriter, Player.Black);
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameSession {gameCode}] Error en HumanVsAI: {ex}");
        }
    }

    private Move GenerateAIMove()
    {
        var allValidMoves = gameState.GetLegalMovesForPlayer(Player.Black).ToList();
        if (allValidMoves.Count == 0) return null;

        var random = new Random();
        return allValidMoves[random.Next(allValidMoves.Count)];
    }


    private async Task HandlePlayerMessages(StreamReader reader, StreamWriter opponentWriter, Player playerColor)
    {
        try
        {
            while (!isGameOver && reader.BaseStream.CanRead)
            {
                var message = await reader.ReadLineAsync();
                if (message == null)
                {
                    Console.WriteLine($"[GameSession {gameCode}] Cliente {playerColor} desconectado");
                    break;
                }

                var request = JsonSerializer.Deserialize<RequestMessage>(message);
                if (request == null) continue;

                Console.WriteLine($"[GameSession {gameCode}] Procesando comando {request.Command} de {playerColor}");

                switch (request.Command)
                {
                    case "GAME_ACTION_MOVE":
                        await HandleMoveAsync(request, opponentWriter, playerColor);
                        break;

                    default:
                        Console.WriteLine($"[GameSession {gameCode}] Comando no reconocido: {request.Command}");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            if (!isGameOver)
            {
                Console.WriteLine($"[GameSession {gameCode}] Error inesperado en {playerColor}: {ex}");
                await NotifyDisconnection(opponentWriter, playerColor);
            }
        }
    }

    private async Task HandleMoveAsync(RequestMessage message, StreamWriter opponentWriter, Player playerColor)
    {
        try
        {
            Console.WriteLine($"[GameSession {gameCode}] Procesando movimiento de {playerColor}");

            if (gameState.CurrentPlayer != playerColor)
            {
                Console.WriteLine($"[GameSession {gameCode}] No es el turno de {playerColor}");
                var errorResponse = new ResponseMessage
                {
                    Status = "MOVE_ERROR",
                    Message = "No es tu turno"
                };
                await opponentWriter.WriteLineAsync(JsonSerializer.Serialize(errorResponse));
                return;
            }

            var move = ParseMove(message.Data);
            Console.WriteLine($"[GameSession {gameCode}] Movimiento recibido: {move.FromPos} → {move.ToPos}");

            var result = gameState.ExecuteMove(move);

            if (!result.IsValid)
            {
                Console.WriteLine($"[GameSession {gameCode}] Movimiento inválido: {result.ErrorMessage}");
                var errorResponse = new ResponseMessage
                {
                    Status = "MOVE_ERROR",
                    Message = result.ErrorMessage ?? "Movimiento no válido"
                };
                await opponentWriter.WriteLineAsync(JsonSerializer.Serialize(errorResponse));
                return;
            }

            // Notificar al jugador que su movimiento fue aceptado
            var response = new ResponseMessage
            {
                Status = "MOVE_ACCEPTED",
                Data = new Dictionary<string, string>
            {
                {"fromRow", move.FromPos.Row.ToString()},
                {"fromCol", move.FromPos.Column.ToString()},
                {"toRow", move.ToPos.Row.ToString()},
                {"toCol", move.ToPos.Column.ToString()}
            }
            };
            await opponentWriter.WriteLineAsync(JsonSerializer.Serialize(response));

            if (result.GameOver)
            {
                await NotifyGameEnd(opponentWriter, result);
            }
            else if (this.isAgainstAI && playerColor == Player.White)
            {
                // Solo si es contra IA y fue el turno del jugador humano
                await Task.Delay(500); // Pequeña pausa para simular pensamiento de la IA

                var aiMove = GenerateAIMove();
                if (aiMove != null)
                {
                    Console.WriteLine($"[GameSession {gameCode}] IA moviendo: {aiMove.FromPos} → {aiMove.ToPos}");

                    var aiResult = gameState.ExecuteMove(aiMove);
                    if (aiResult.IsValid)
                    {
                        var aiResponse = new ResponseMessage
                        {
                            Status = "OPPONENT_MOVE",
                            Data = new Dictionary<string, string>
                        {
                            {"fromRow", aiMove.FromPos.Row.ToString()},
                            {"fromCol", aiMove.FromPos.Column.ToString()},
                            {"toRow", aiMove.ToPos.Row.ToString()},
                            {"toCol", aiMove.ToPos.Column.ToString()}
                        }
                        };
                        await opponentWriter.WriteLineAsync(JsonSerializer.Serialize(aiResponse));
                    }
                }
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameSession {gameCode}] Error procesando movimiento: {ex}");
        }
    }

    private Move ParseMove(Dictionary<string, string> moveData)
    {
        try
        {
            int fromRow = int.Parse(moveData["fromRow"]);
            int fromCol = int.Parse(moveData["fromCol"]);
            int toRow = int.Parse(moveData["toRow"]);
            int toCol = int.Parse(moveData["toCol"]);

            Console.WriteLine($"[GameSession {gameCode}] Parseando movimiento: ({fromRow},{fromCol}) → ({toRow},{toCol})");

            return new NormalMove(
                new Position(fromRow, fromCol),
                new Position(toRow, toCol)
            );
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameSession {gameCode}] Error parseando movimiento: {ex}");
            throw new ArgumentException("Datos de movimiento inválidos");
        }
    }

    private async Task SendStartMessages(StreamWriter whiteWriter, StreamWriter? blackWriter, Dictionary<Player, int> players)
    {
        var whitePlayer = players[Player.White];
        var blackPlayer = players[Player.Black];

        await SendJsonAsync(whiteWriter, "GAME_START", "Eres las blancas", new Dictionary<string, string>
    {
        { "color", "white" },
        { "opponentId", blackPlayer.ToString() },
        { "gameCode", gameCode }
    });

        if (blackWriter != null)
        {
            await SendJsonAsync(blackWriter, "GAME_START", "Eres las negras", new Dictionary<string, string>
        {
            { "color", "black" },
            { "opponentId", whitePlayer.ToString() },
            { "gameCode", gameCode }
        });
        }
    }

    private async Task NotifyGameEnd(StreamWriter writer, MoveResult result)
    {
        var endData = new Dictionary<string, string>
        {
            { "winner", result.Winner?.ToString() ?? "draw" },
            { "reason", result.GameOverReason },
            { "duration", result.GameDuration?.ToString() ?? "00:00:00" }
        };

        await SendJsonAsync(writer, "GAME_OVER", "La partida ha terminado", endData);
        isGameOver = true;
    }

    private async Task NotifyDisconnection(StreamWriter writer, Player disconnectedPlayer)
    {
        await SendJsonAsync(writer, "OPPONENT_DISCONNECTED", $"El oponente ({disconnectedPlayer}) se ha desconectado");
        isGameOver = true;
    }

    private async Task SendJsonAsync(StreamWriter writer, string status, string message, Dictionary<string, string>? data = null)
    {
        var response = new ResponseMessage
        {
            Status = status,
            Message = message,
            Data = data
        };
        await writer.WriteLineAsync(JsonSerializer.Serialize(response));
    }
}