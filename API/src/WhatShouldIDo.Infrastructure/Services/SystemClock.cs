using WhatShouldIDo.Application.Interfaces;

namespace WhatShouldIDo.Infrastructure.Services
{
    /// <summary>
    /// Production implementation of IClock that returns the actual system time.
    /// </summary>
    public sealed class SystemClock : IClock
    {
        /// <summary>
        /// Singleton instance for convenience (IClock is stateless).
        /// </summary>
        public static readonly SystemClock Instance = new();

        /// <inheritdoc />
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
