using Microsoft.Extensions.Logging;

namespace GameGaraj.Shared.Logging
{
    public class FileLogger : ILogger
    {
        private readonly string _categoryName;
        private readonly string _logFilePath;
        private static readonly object _lock = new object();

        public FileLogger(string categoryName, string logFilePath)
        {
            _categoryName = categoryName;
            _logFilePath = logFilePath;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => true;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
                return;

            var logMessage = $"[{DateTime.Now:yyyy-MM-dd HH:mm:ss.fff}] [{logLevel}] [{_categoryName}] {formatter(state, exception)}";
            
            if (exception != null)
            {
                logMessage += Environment.NewLine + exception.ToString();
            }

            lock (_lock)
            {
                File.AppendAllText(_logFilePath, logMessage + Environment.NewLine);
            }
        }
    }

    public class FileLoggerProvider : ILoggerProvider
    {
        private readonly string _logFilePath;

        public FileLoggerProvider(string serviceName)
        {
            var logsDirectory = Path.Combine(Directory.GetCurrentDirectory(), "..", "ConsoleLogs");
            Directory.CreateDirectory(logsDirectory);
            
            _logFilePath = Path.Combine(logsDirectory, $"{serviceName}.txt");
            
            // Her çalıştırmada dosyayı temizle (override)
            if (File.Exists(_logFilePath))
            {
                File.Delete(_logFilePath);
            }
        }

        public ILogger CreateLogger(string categoryName)
        {
            return new FileLogger(categoryName, _logFilePath);
        }

        public void Dispose() { }
    }

    public static class FileLoggerExtensions
    {
        public static ILoggingBuilder AddFileLogger(this ILoggingBuilder builder, string serviceName)
        {
            builder.AddProvider(new FileLoggerProvider(serviceName));
            return builder;
        }
    }
}
