using System;

namespace TaxKeepVN.Application.Exceptions
{
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class ServiceUnavailableException : Exception
    {
        public string ErrorCode { get; }

        public ServiceUnavailableException(string errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }

        public ServiceUnavailableException(string message) : base(message)
        {
            ErrorCode = "SERVICE_UNAVAILABLE";
        }
    }
}
