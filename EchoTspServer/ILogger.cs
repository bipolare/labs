// ILogger.cs
// -----------------------------
// Simple logging abstraction.
// Enables replacing logging implementation in tests
// to assert messages or suppress console output.

namespace EchoTspServer
{
    // Простий логгер як абстракція.
    // У тестах замінюємо на мок.
    public interface ILogger
    {
        void Log(string message);
        void LogError(string message);
    }
}
