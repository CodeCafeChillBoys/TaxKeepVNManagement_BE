using System;

namespace TaxKeepVN.Application.Exceptions
{
[System.Diagnostics.CodeAnalysis.ExcludeFromCodeCoverage]
    public class NotFoundException : Exception
    {
        public NotFoundException(string message) : base(message) { }
    }
}
