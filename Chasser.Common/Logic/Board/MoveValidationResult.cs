using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Chasser.Common.Logic.Board
{
    public class MoveValidationResult
    {
        public bool IsValid { get; }
        public string ErrorMessage { get; }

        private MoveValidationResult(bool isValid, string errorMessage = null)
        {
            IsValid = isValid;
            ErrorMessage = errorMessage;
        }

        public static MoveValidationResult Valid() => new MoveValidationResult(true);
        public static MoveValidationResult Invalid(string message) => new MoveValidationResult(false, message);
    }
}
