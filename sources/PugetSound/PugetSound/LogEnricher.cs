using System;
using Serilog.Core;
using Serilog.Events;

namespace PugetSound
{
    public class LogEnricher : ILogEventEnricher
    {
        private static readonly LogEventProperty ProjectProperty;
        private static readonly LogEventProperty SlotProperty;
        private static readonly LogEventProperty VersionProperty;

        static LogEnricher()
        {
            ProjectProperty = new LogEventProperty("$project", new ScalarValue("pugetsound"));

            var slotEnvVar  = Environment.GetEnvironmentVariable("PugetSoundSlot");

            if (string.IsNullOrWhiteSpace(slotEnvVar)) slotEnvVar = "local";

            SlotProperty = new LogEventProperty("$slot", new ScalarValue(slotEnvVar));

            VersionProperty = new LogEventProperty("$version", new ScalarValue(Revision.Footer));
        }

        public void Enrich(LogEvent logEvent, ILogEventPropertyFactory propertyFactory)
        {
            logEvent.AddPropertyIfAbsent(ProjectProperty);
            logEvent.AddPropertyIfAbsent(SlotProperty);
            logEvent.AddPropertyIfAbsent(VersionProperty);
        }
    }
}
