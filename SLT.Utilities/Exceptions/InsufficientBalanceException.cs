using Utilities.Enums;
using Utilities.Exceptions.Common;
using SLT.Utilities.Exceptions;

namespace Utilities.Exceptions
{
    public class InsufficientBalanceException : BadRequestException
    {
        public InsufficientBalanceException()
          : base(ApiResultStatusCode.InsufficientBalance, ExceptionMessages.NonceNotFoundException)
        {
        }
    }
}
