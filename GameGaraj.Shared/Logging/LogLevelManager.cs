using System.Collections.Concurrent;
using Serilog.Core;
using Serilog.Events;

namespace GameGaraj.Shared.Logging
{
    /// <summary>
    /// Manages runtime log level switches per service.
    /// Allows changing log level without redeployment using Serilog's LoggingLevelSwitch.
    /// </summary>
    public static class LogLevelManager
    {
        private static readonly ConcurrentDictionary<string, LoggingLevelSwitch> _switches = new();

        /// <summary>
        /// Gets or creates a LoggingLevelSwitch for the specified service.
        /// Default level is Information.
        /// </summary>
        public static LoggingLevelSwitch GetSwitch(string serviceName)
        {
            return _switches.GetOrAdd(serviceName, _ => new LoggingLevelSwitch(LogEventLevel.Information));
        }

        /// <summary>
        /// Sets the minimum log level for a service at runtime.
        /// </summary>
        public static void SetLevel(string serviceName, LogEventLevel level)
        {
            GetSwitch(serviceName).MinimumLevel = level;
        }

        /// <summary>
        /// Gets the current minimum log level for a service.
        /// </summary>
        public static LogEventLevel GetLevel(string serviceName)
        {
            return GetSwitch(serviceName).MinimumLevel;
        }

        /// <summary>
        /// Returns all registered service log levels.
        /// </summary>
        public static IReadOnlyDictionary<string, LogEventLevel> GetAllLevels()
        {
            return _switches.ToDictionary(x => x.Key, x => x.Value.MinimumLevel);
        }
    }
}
