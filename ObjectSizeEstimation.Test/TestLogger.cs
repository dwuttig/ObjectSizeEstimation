using Microsoft.Extensions.Logging;

namespace ObjectSizeEstimation.Test;

public class TestLogger : ILogger
{
    public List<string> LogEntries { get; } = new ();

    public IDisposable? BeginScope<TState>(TState state)
        where TState : notnull => null;

    public bool IsEnabled(LogLevel logLevel) => true;

    public void Log<TState>(
        LogLevel logLevel,
        EventId eventId,
        TState state,
        Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var message = formatter(state, exception);
        LogEntries.Add(message);
    }
}