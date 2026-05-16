using System;

namespace EdgeCalendar.Infrastructure
{
    public sealed class ConflictException : Exception
    {
        public ConflictException(string message, string? serverJson = null, Exception? innerException = null)
            : base(message, innerException)
        {
            ServerJson = serverJson;
        }

        public string? ServerJson { get; }
    }
}
