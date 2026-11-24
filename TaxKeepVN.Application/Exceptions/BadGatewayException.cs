using System;

namespace TaxKeepVN.Application.Exceptions
{
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class BadGatewayException : Exception
    {
        public string ErrorCode { get; }

        public BadGatewayException(string errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }

        public BadGatewayException(string message) : base(message)
        {
            ErrorCode = "BAD_GATEWAY";
        }
    }
}
