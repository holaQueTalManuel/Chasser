using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
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
        private GameSessionLogic sessionLogic = new GameSessionLogic();


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

            var localReader = new StreamReader(stream1);
            var localWriter = new StreamWriter(stream1) { AutoFlush = true };
            var OppReader = new StreamReader(stream2);
            var OppWriter = new StreamWriter(stream2) { AutoFlush = true };

            await SendJsonAsync(localWriter, "START_GAME", Player.White.ToString(), new Dictionary<string, string> { { "opponent", Player.Black.ToString() } });
            await SendJsonAsync(OppWriter, "START_GAME", Player.Black.ToString(), new Dictionary<string, string> { { "opponent", Player.White.ToString() } });

            var task1 = RelayMessages(localReader, OppWriter);
            var task2 = RelayMessages(OppReader, localWriter);

            await Task.WhenAny(task1, task2); // Termina si alguno se desconecta
            isGameOver = true;
        }
        private async Task RelayMessages(StreamReader readerFrom, StreamWriter writerTo)
        {
            try
            {
                while (!isGameOver)
                {
                    var line = await readerFrom.ReadLineAsync();
                    if (line == null)
                    {
                        break; // Se desconectó
                    }

                    // Asumimos que el mensaje contiene un movimiento en formato JSON
                    var message = JsonSerializer.Deserialize<RequestMessage>(line);
                    if (message != null && message.Data.TryGetValue("type", out string type) && type == "MOVE")
                    {
                        await HandleMoveAsync(message.ToString(), writerTo);
                    }
                    else
                    {
                        // Si no es un movimiento, se puede enviar un error
                        await SendJsonAsync(writerTo, "ERROR", "Mensaje no reconocido");
                    }
                }
            }
            catch
            {
                // Se rompe si hay un error de red
            }
            finally
            {
                isGameOver = true;
            }
        }


        private async Task HandleMoveAsync(string line, StreamWriter writerTo)
        {
            var message = JsonSerializer.Deserialize<RequestMessage>(line);

            // Construye el movimiento a partir del mensaje recibido
            var move = new NormalMove(
                new Position(int.Parse(message.Data["fromRow"]), int.Parse(message.Data["fromCol"])),
                new Position(int.Parse(message.Data["toRow"]), int.Parse(message.Data["toCol"]))
            );

            if (sessionLogic.TryMakeMove(move, out string error))
            {
                // Reenvía el movimiento al otro jugador
                await writerTo.WriteLineAsync(line);

                if (sessionLogic.IsGameOver())
                {
                    await writerTo.WriteLineAsync("GAME_OVER");
                    // Puedes notificar también al jugador que envió el movimiento
                }
            }
            else
            {
                // Notificar error al cliente
                await writerTo.WriteLineAsync(JsonSerializer.Serialize(new ResponseMessage
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

            string json = JsonSerializer.Serialize(response);
            await writer.WriteLineAsync(json);
        }
    }
}
