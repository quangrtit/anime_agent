using System;
using System.Collections.Generic;

namespace AnimeAssistant.Diagnostics
{
    public sealed class StructuredLogEvent
    {
        public StructuredLogEvent(string level, string eventName, IReadOnlyDictionary<string, object> fields)
        {
            TimestampUtc = DateTime.UtcNow;
            Level = level;
            EventName = eventName;
            Fields = fields;
        }

        public DateTime TimestampUtc { get; }
        public string Level { get; }
        public string EventName { get; }
        public IReadOnlyDictionary<string, object> Fields { get; }
    }

    public interface IStructuredLogSink
    {
        void Write(StructuredLogEvent logEvent);
    }
}

