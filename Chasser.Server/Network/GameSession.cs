using System;
using System.Collections.Generic;
using System.IO;
using System.Net.Sockets;
using System.Text.Json;
using System.Threading.Tasks;
using Chasser.Common.Logic;
using Chasser.Common.Network;
using Chasser.Logic.Board;
using Chasser.Moves;

namespace Chasser.Server.Network
{
    public class GameSession
    {
        private TcpClient player1;
        private TcpClient player2;
        private bool isGameOver = false;
        private GameSessionLogic sessionLogic = new();

        public GameSession(TcpClient p1, TcpClient p2)
        {
            player1 = p1;
            player2 = p2;
            _ = RunSessionAsync();
        }

        private async Task RunSessionAsync()
        {
            using var stream1 = player1.GetStream();
            using var stream2 = player2.GetStream();

            var reader1 = new StreamReader(stream1);
            var writer1 = new StreamWriter(stream1) { AutoFlush = true };

            var reader2 = new StreamReader(stream2);
            var writer2 = new StreamWriter(stream2) { AutoFlush = true };

            await SendJsonAsync(writer1, "START_GAME", "White", new() { { "opponent", "Black" } });
            await SendJsonAsync(writer2, "START_GAME", "Black", new() { { "opponent", "White" } });

            var task1 = RelayMessages(reader1, writer2);
            var task2 = RelayMessages(reader2, writer1);

            await Task.WhenAny(task1, task2);
            isGameOver = true;
        }

        private async Task RelayMessages(StreamReader reader, StreamWriter writer)
        {
            try
            {
                while (!isGameOver)
                {
                    var line = await reader.ReadLineAsync();
                    if (line == null) break;

                    var message = JsonSerializer.Deserialize<RequestMessage>(line);
                    if (message != null && message.Data.TryGetValue("type", out string type) && type == "MOVE")
                    {
                        await HandleMoveAsync(message, writer);
                    }
                    else
                    {
                        await SendJsonAsync(writer, "ERROR", "Mensaje no reconocido");
                    }
                }
            }
            catch
            {
                // Silenciar errores por desconexión
            }
            finally
            {
                isGameOver = true;
            }
        }

        private async Task HandleMoveAsync(RequestMessage message, StreamWriter writer)
        {
            var move = new NormalMove(
                new Position(int.Parse(message.Data["fromRow"]), int.Parse(message.Data["fromCol"])),
                new Position(int.Parse(message.Data["toRow"]), int.Parse(message.Data["toCol"]))
            );

            if (sessionLogic.TryMakeMove(move, out string error))
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(message));
                if (sessionLogic.IsGameOver())
                    await writer.WriteLineAsync("GAME_OVER");
            }
            else
            {
                await writer.WriteLineAsync(JsonSerializer.Serialize(new ResponseMessage
                {
                    Status = "ERROR",
                    Message = error
                }));
            }
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
}
