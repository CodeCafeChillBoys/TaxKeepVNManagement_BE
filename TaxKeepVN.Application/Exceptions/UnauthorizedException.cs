using System;

namespace TaxKeepVN.Application.Exceptions
{
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class UnauthorizedException : Exception
    {
        public string ErrorCode { get; }

        public UnauthorizedException(string errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}
