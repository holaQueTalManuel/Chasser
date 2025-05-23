﻿
using Chasser.Logic.Enums;

namespace Chasser.Logic.Board
{
    public class Result
    {
        public Player Winner { get; }
        public EndReason Reason { get; }

        public Result(Player winner, EndReason reason)
        {
            Winner = winner;
            Reason = reason;
        }
        public static Result WinA(Player winner)
        {
            return new Result(winner, EndReason.JackSen);
        }
        public static Result WinB(Player winner)
        {
            return new Result(winner, EndReason.Ñam);
        }
    }
}
