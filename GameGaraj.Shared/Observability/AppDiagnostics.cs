using System;
using System.Diagnostics;

namespace GameGaraj.Shared.Observability
{
    /// <summary>
    /// Central manual trace helper for all GameGaraj applications.
    /// Uses the runtime SERVICE_NAME environment variable to dynamically bind the correct ActivitySource.
    /// </summary>
    public static class AppDiagnostics
    {
        private static ActivitySource? _activitySource;

        public static ActivitySource ActivitySource
        {
            get
            {
                if (_activitySource == null)
                {
                    var serviceName = Environment.GetEnvironmentVariable("SERVICE_NAME") ?? "GameGaraj.App";
                    _activitySource = new ActivitySource(serviceName);
                }
                return _activitySource;
            }
        }

        public static Activity? StartActivity(string name, ActivityKind kind = ActivityKind.Internal)
        {
            return ActivitySource.StartActivity(name, kind);
        }

        public static Activity? StartActivity(string name, ActivityKind kind, ActivityContext parentContext)
        {
            return ActivitySource.StartActivity(name, kind, parentContext);
        }
    }
}
