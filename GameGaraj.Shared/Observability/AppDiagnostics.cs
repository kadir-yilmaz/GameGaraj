using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Runtime.CompilerServices;

namespace GameGaraj.Shared.Observability
{
    /// <summary>
    /// Central manual trace helper for all GameGaraj applications.
    /// Uses the runtime SERVICE_NAME environment variable to dynamically bind the correct ActivitySource.
    /// </summary>
    public static class AppDiagnostics
    {
        private static ActivitySource? _activitySource;
        private static readonly HashSet<string> LowValueManualSpanNames = new(StringComparer.OrdinalIgnoreCase)
        {
            "Build Order Pricing Snapshot",
            "Build Payment Pricing Snapshot",
            "Build Payment Request",
            "Build Iyzico Request",
            "Build Order Aggregate"
        };

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

        public static Activity? StartActivity(
            string name,
            ActivityKind kind = ActivityKind.Internal,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "")
        {
            if (ShouldSuppressManualSpan(name))
            {
                return null;
            }

            var activity = ActivitySource.StartActivity(name, kind);
            EnrichActivity(activity, callerMemberName, callerFilePath);
            return activity;
        }

        public static Activity? StartActivity(
            string name,
            ActivityKind kind,
            ActivityContext parentContext,
            [CallerMemberName] string callerMemberName = "",
            [CallerFilePath] string callerFilePath = "")
        {
            if (ShouldSuppressManualSpan(name))
            {
                return null;
            }

            var activity = ActivitySource.StartActivity(name, kind, parentContext);
            EnrichActivity(activity, callerMemberName, callerFilePath);
            return activity;
        }

        private static bool ShouldSuppressManualSpan(string name)
        {
            var suppressLowValueSpans = Environment.GetEnvironmentVariable("OpenTelemetry__SuppressLowValueManualSpans");
            if (string.Equals(suppressLowValueSpans, "false", StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            return LowValueManualSpanNames.Contains(name);
        }

        private static void EnrichActivity(Activity? activity, string callerMemberName, string callerFilePath)
        {
            if (activity == null)
            {
                return;
            }

            var sourceFileName = Path.GetFileNameWithoutExtension(callerFilePath);
            activity.SetTag("app.span.category", "business");
            activity.SetTag("code.function", callerMemberName);

            if (!string.IsNullOrWhiteSpace(sourceFileName))
            {
                activity.SetTag("code.namespace", sourceFileName);
            }
        }
    }
}
