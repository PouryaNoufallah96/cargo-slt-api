using SLT.Utilities.Exceptions;
using Utilities.Enums;
using Utilities.Exceptions.Common;

namespace Utilities.Exceptions
{
    public class NonceNotFoundException : NotFoundException
    {
        public NonceNotFoundException()
          : base(ApiResultStatusCode.NonceNotFound ,ExceptionMessages.NonceNotFoundException)
        {
        }        
    }
}
