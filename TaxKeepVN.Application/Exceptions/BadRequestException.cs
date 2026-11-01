using System;

namespace TaxKeepVN.Application.Exceptions
{
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class BadRequestException : Exception
    {
        public string ErrorCode { get; }
        public BadRequestException(string errorCode, string message) : base(message) 
        { 
            ErrorCode = errorCode;
        }
    }
}
