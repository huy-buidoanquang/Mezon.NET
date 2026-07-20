using System;
using System.IO;
using System.Text;

namespace Mezon.Net.Sdk.Caching.Sqlite
{
    /// <summary>
    ///     Helpers for one-database-per-account-and-environment layout. Callers choose the base directory;
    ///     this type does not touch <c>.gitignore</c> or assume shared network paths.
    /// </summary>
    public static class SqliteMessageStorePaths
    {
        public static string ResolveDatabasePath(string baseDirectory, string accountId, string environment)
        {
            if (string.IsNullOrWhiteSpace(baseDirectory))
            {
                throw new ArgumentException("Base directory is required.", nameof(baseDirectory));
            }

            if (string.IsNullOrWhiteSpace(accountId))
            {
                throw new ArgumentException("Account id is required.", nameof(accountId));
            }

            if (string.IsNullOrWhiteSpace(environment))
            {
                throw new ArgumentException("Environment is required.", nameof(environment));
            }

            var fileName = $"{SanitizeFileToken(accountId)}_{SanitizeFileToken(environment)}.db";
            return Path.Combine(baseDirectory, fileName);
        }

        internal static string SanitizeFileToken(string value)
        {
            var builder = new StringBuilder(value.Length);
            foreach (var ch in value.Trim())
            {
                if (char.IsLetterOrDigit(ch) || ch is '-' or '_' or '.')
                {
                    builder.Append(ch);
                }
                else
                {
                    builder.Append('_');
                }
            }

            var sanitized = builder.ToString();
            return sanitized.Length == 0 ? "default" : sanitized;
        }
    }
}
