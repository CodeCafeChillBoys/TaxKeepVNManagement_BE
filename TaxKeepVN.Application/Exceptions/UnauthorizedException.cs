using System;

namespace TaxKeepVN.Application.Exceptions
{
    public class UnauthorizedException : Exception
    {
        public string ErrorCode { get; }

        public UnauthorizedException(string errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}
