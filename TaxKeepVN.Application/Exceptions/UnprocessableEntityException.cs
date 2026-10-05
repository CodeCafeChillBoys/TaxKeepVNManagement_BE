using System;

namespace TaxKeepVN.Application.Exceptions
{
    public class UnprocessableEntityException : Exception
    {
        public string ErrorCode { get; }

        public UnprocessableEntityException(string errorCode, string message) : base(message)
        {
            ErrorCode = errorCode;
        }
    }
}
