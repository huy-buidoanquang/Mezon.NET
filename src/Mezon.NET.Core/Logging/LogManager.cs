using System;
using System.Threading.Tasks;
using Mezon.Net.Core;

namespace Mezon.Net.Logging
{
    public class LogManager
    {
        public LogLevel Level { get; }
        private Logger ClientLogger { get; }

        public event Func<LogMessage, Task> Message { add { _messageEvent.Add(value); } remove { _messageEvent.Remove(value); } }
        private readonly AsyncEvent<Func<LogMessage, Task>> _messageEvent = new AsyncEvent<Func<LogMessage, Task>>();

        public LogManager(LogLevel minLevel)
        {
            Level = minLevel;
            ClientLogger = new Logger(this, "Mezon.Net");
        }

        public async Task LogAsync(LogLevel severity, string source, Exception? ex)
        {
            try
            {
                if (severity >= Level)
                {
                    await _messageEvent.InvokeAsync(new LogMessage(severity, source, string.Empty, ex)).ConfigureAwait(false);
                }
            }
            catch
            {
                // ignored
            }
        }
        public async Task LogAsync(LogLevel severity, string source, string message, Exception? ex = null)
        {
            try
            {
                if (severity >= Level)
                {
                    await _messageEvent.InvokeAsync(new LogMessage(severity, source, message, ex)).ConfigureAwait(false);
                }
            }
            catch
            {
                // ignored
            }
        }
        public async Task LogAsync(LogLevel severity, string source, FormattableString message, Exception? ex = null)
        {
            try
            {
                if (severity >= Level)
                {
                    await _messageEvent.InvokeAsync(new LogMessage(severity, source, message.ToString(), ex)).ConfigureAwait(false);
                }
            }
            catch { }
        }
        public Task ErrorAsync(string source, Exception? ex)
            => LogAsync(LogLevel.Error, source, ex);
        public Task ErrorAsync(string source, string message, Exception? ex = null)
            => LogAsync(LogLevel.Error, source, message, ex);
        public Task ErrorAsync(string source, FormattableString message, Exception? ex = null)
            => LogAsync(LogLevel.Error, source, message, ex);
        public Task WarningAsync(string source, Exception? ex)
            => LogAsync(LogLevel.Warning, source, ex);
        public Task WarningAsync(string source, string message, Exception? ex = null)
            => LogAsync(LogLevel.Warning, source, message, ex);
        public Task WarningAsync(string source, FormattableString message, Exception? ex = null)
            => LogAsync(LogLevel.Warning, source, message, ex);
        public Task InfoAsync(string source, Exception? ex)
            => LogAsync(LogLevel.Information, source, ex);
        public Task InfoAsync(string source, string message, Exception? ex = null)
            => LogAsync(LogLevel.Information, source, message, ex);
        public Task InfoAsync(string source, FormattableString message, Exception? ex = null)
            => LogAsync(LogLevel.Information, source, message, ex);
        public Task DebugAsync(string source, Exception? ex)
            => LogAsync(LogLevel.Debug, source, ex);
        public Task DebugAsync(string source, string message, Exception? ex = null)
            => LogAsync(LogLevel.Debug, source, message, ex);
        public Task DebugAsync(string source, FormattableString message, Exception? ex = null)
            => LogAsync(LogLevel.Debug, source, message, ex);
        public Task TraceAsync(string source, Exception? ex)
            => LogAsync(LogLevel.Trace, source, ex);
        public Task TraceAsync(string source, string message, Exception? ex = null)
            => LogAsync(LogLevel.Trace, source, message, ex);
        public Task TraceAsync(string source, FormattableString message, Exception? ex = null)
            => LogAsync(LogLevel.Trace, source, message, ex);
        public Logger CreateLogger(string name) => new Logger(this, name);
        public Task WriteInitialLog()
            => ClientLogger.InfoAsync($"Mezon.Net v1.0.1");
    }
}
