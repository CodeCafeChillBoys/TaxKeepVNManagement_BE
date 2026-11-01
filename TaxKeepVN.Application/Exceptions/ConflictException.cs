using System;

namespace TaxKeepVN.Application.Exceptions
{
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class ConflictException : Exception
    {
        public string ErrorCode { get; }

        public ConflictException(string errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}
