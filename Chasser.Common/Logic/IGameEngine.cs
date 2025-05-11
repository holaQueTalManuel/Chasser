using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using Chasser.Common.Logic.Board;
using Chasser.Common.Logic.Moves;
using Chasser.Common.Network;

namespace Chasser.Common.Logic
{
    public enum GameMode { LocalAI, Multiplayer }

    public interface IGameEngine
    {
        void StartGame(Player playerColor);
        void ProcessMove(Move move);
        event EventHandler<GameStateUpdatedEventArgs> OnGameStateUpdated;
    }
}
