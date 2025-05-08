using Chasser.Common.Logic;
using Chasser.Common.Network;
using Chasser.Logic.Board;
using Chasser.Moves;
using System.Net.Sockets;
using System.Text.Json;

public class GameSession
{
    private readonly TcpClient player1;
    private readonly TcpClient player2;
    private readonly string gameCode;
    private readonly GameState gameState;
    private bool isGameOver = false;

    public GameSession(TcpClient player1, TcpClient player2, string gameCode, int player1Id, int player2Id)
    {
        this.player1 = player1;
        this.player2 = player2;
        this.gameCode = gameCode;

        // Inicializar estado del juego
        gameState = new GameState(Player.White, Board.Initialize());
        gameState.RegisterPlayer(Player.White, player1Id);
        gameState.RegisterPlayer(Player.Black, player2Id);

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
            using var stream2 = player2.GetStream();

            var reader1 = new StreamReader(stream1);
            var writer1 = new StreamWriter(stream1) { AutoFlush = true };

            var reader2 = new StreamReader(stream2);
            var writer2 = new StreamWriter(stream2) { AutoFlush = true };

            Console.WriteLine($"[GameSession {gameCode}] Streams abiertos. Iniciando partida.");

            await SendStartMessages(writer1, writer2, gameState.Players);

            Console.WriteLine($"[GameSession {gameCode}] Mensajes de inicio enviados a ambos jugadores.");

            var task1 = HandlePlayerMessages(reader1, writer2, Player.White);
            var task2 = HandlePlayerMessages(reader2, writer1, Player.Black);

            await Task.WhenAny(task1, task2);
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
                return;
            }

            var move = ParseMove(message.Data);
            Console.WriteLine($"[GameSession {gameCode}] Movimiento recibido: {move.FromPos} → {move.ToPos}");

            var result = gameState.ExecuteMove(move);

            if (!result.IsValid)
            {
                Console.WriteLine($"[GameSession {gameCode}] Movimiento inválido: {result.ErrorMessage}");
                return;
            }

            // Notificar al oponente
            var response = new ResponseMessage
            {
                Status = "OPPONENT_MOVE",
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
        }
        catch (Exception ex)
        {
            Console.WriteLine($"[GameSession {gameCode}] Error procesando movimiento: {ex}");
        }
    }

    private Move ParseMove(Dictionary<string, string> moveData)
    {
        return new NormalMove(
            new Position(int.Parse(moveData["fromRow"]), int.Parse(moveData["fromCol"])),
            new Position(int.Parse(moveData["toRow"]), int.Parse(moveData["toCol"]))
        );
    }

    private async Task SendStartMessages(StreamWriter whiteWriter, StreamWriter blackWriter, Dictionary<Player, int> players)
    {
        var whitePlayer = players[Player.White];
        var blackPlayer = players[Player.Black];

        await SendJsonAsync(whiteWriter, "GAME_START", "Eres las blancas", new Dictionary<string, string>
        {
            { "color", "white" },
            { "opponentId", blackPlayer.ToString() },
            { "gameCode", gameCode }
        });

        await SendJsonAsync(blackWriter, "GAME_START", "Eres las negras", new Dictionary<string, string>
        {
            { "color", "black" },
            { "opponentId", whitePlayer.ToString() },
            { "gameCode", gameCode }
        });
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