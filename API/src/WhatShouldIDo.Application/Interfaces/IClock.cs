namespace WhatShouldIDo.Application.Interfaces
{
    /// <summary>
    /// Abstraction for time to enable deterministic testing.
    /// All time-sensitive operations should use this instead of DateTime.UtcNow directly.
    /// </summary>
    public interface IClock
    {
        /// <summary>
        /// Gets the current UTC time.
        /// </summary>
        DateTime UtcNow { get; }
    }
}
