using System;

namespace TaxKeepVN.Application.Exceptions
{
    public class ForbiddenException : Exception
    {
        public string ErrorCode { get; }

        public ForbiddenException(string message) : base(message)
        {
            ErrorCode = "FORBIDDEN";
        }

        public ForbiddenException(string errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}
