using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Chasser.Common.Logic.Board;

namespace Chasser.Common.Logic
{
    public class GameStateUpdatedEventArgs : EventArgs
    {
        public GameState GameState { get; }

        public GameStateUpdatedEventArgs(GameState gameState)
        {
            GameState = gameState;
        }
    }

}
