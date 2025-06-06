﻿using Microsoft.Extensions.Logging;

namespace VpnHood.Core.Toolkit.Logging;

public class VhConsoleLogger(bool includeScopes = true, bool singleLine = true, string? categoryName = null)
    : TextLogger(includeScopes, categoryName)
{
    private static bool? _isColorSupported;
    private readonly object _lock = new();

    private static bool IsColorSupported {
        get {
            if (_isColorSupported == null) {
                try {
                    _ = Console.ForegroundColor;
                    _isColorSupported = true;
                }
                catch {
                    _isColorSupported = false;
                }
            }

            return _isColorSupported.Value;
        }
    }

    public override void Log<TState>(LogLevel logLevel, EventId eventId, TState state, Exception? exception,
        Func<TState, Exception?, string> formatter)
    {
        var text = FormatLog(logLevel, eventId, state, exception, formatter);
        if (singleLine)
            text = text.Replace("\n", " ").Replace("\r", "").Trim();

        lock (_lock) {
            if (IsColorSupported) {
                var prevColor = Console.ForegroundColor;
                Console.ForegroundColor = GetColor(logLevel);
                Console.WriteLine(text);
                Console.ForegroundColor = prevColor;
            }
            else {
                Console.WriteLine(text);
            }
        }
    }

    public ConsoleColor GetColor(LogLevel logLevel)
    {
        return logLevel switch {
            LogLevel.Trace => ConsoleColor.Gray,
            LogLevel.Debug => ConsoleColor.Gray,
            LogLevel.Information => ConsoleColor.White,
            LogLevel.Warning => ConsoleColor.Yellow,
            LogLevel.Error => ConsoleColor.Red,
            LogLevel.Critical => ConsoleColor.DarkRed,
            _ => ConsoleColor.White
        };
    }
}