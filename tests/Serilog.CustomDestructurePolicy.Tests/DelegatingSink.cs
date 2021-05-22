using System;
using Serilog.Core;
using Serilog.Events;

namespace Serilog.CustomDestructurePolicy.Tests
{
    public class DelegatingSink : ILogEventSink
    {
        private readonly Action<LogEvent> _write;

        public DelegatingSink(Action<LogEvent> write)
        {
            _write = write;
        }

        public void Emit(LogEvent logEvent) => _write(logEvent);
    }
}
