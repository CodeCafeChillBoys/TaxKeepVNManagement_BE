using System;

namespace TaxKeepVN.Application.Exceptions
{
    public class BadRequestException : Exception
    {
        public string ErrorCode { get; }
        public BadRequestException(string errorCode, string message) : base(message) 
        { 
            ErrorCode = errorCode;
        }
    }
}
