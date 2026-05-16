using System;

namespace EdgeCalendar.Infrastructure
{
    public sealed class GoogleCredentialsMissingException : Exception
    {
        public GoogleCredentialsMissingException()
            : base("Google APIのクライアントID/シークレットが未設定です。")
        {
        }
    }
}
