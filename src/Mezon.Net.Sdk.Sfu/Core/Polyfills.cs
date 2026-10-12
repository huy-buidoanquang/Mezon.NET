#if NETSTANDARD2_1
namespace System.Runtime.CompilerServices
{
    // Records and init-only setters on netstandard2.1.
    internal static class IsExternalInit
    {
    }
}
#endif

namespace Mezon.Net.Sdk.Sfu.Core
{
    using System;
    using System.Diagnostics;
    using System.Security.Cryptography;

    /// <summary>A monotonic clock (time since an arbitrary origin) and saturating deadline arithmetic.</summary>
    internal static class Clock
    {
        private static readonly double TicksPerStopwatchTick = (double)TimeSpan.TicksPerSecond / Stopwatch.Frequency;

        public static TimeSpan Now => TimeSpan.FromTicks((long)(Stopwatch.GetTimestamp() * TicksPerStopwatchTick));

        /// <summary>
        /// <paramref name="now"/> + <paramref name="timeout"/> without overflowing: huge or infinite
        /// (<see cref="System.Threading.Timeout.InfiniteTimeSpan"/>) timeouts saturate to <see cref="TimeSpan.MaxValue"/>.
        /// </summary>
        public static TimeSpan Deadline(TimeSpan now, TimeSpan timeout)
        {
            if (timeout < TimeSpan.Zero || timeout >= TimeSpan.MaxValue - now)
            {
                return TimeSpan.MaxValue;
            }

            return now + timeout;
        }

        public static TimeSpan DeadlineAfter(TimeSpan timeout) => Deadline(Now, timeout);

        /// <summary>A delay usable with Task.Delay/CancelAfter: negative means infinite, huge values are capped.</summary>
        public static TimeSpan Delay(TimeSpan timeout)
        {
            var max = TimeSpan.FromMilliseconds(int.MaxValue - 1);
            if (timeout < TimeSpan.Zero || timeout > max)
            {
                return System.Threading.Timeout.InfiniteTimeSpan;
            }

            return timeout;
        }

        /// <summary>A wait before an action, usable with Task.Delay: negative means none, huge values are capped.</summary>
        public static TimeSpan Wait(TimeSpan delay)
        {
            var max = TimeSpan.FromMilliseconds(int.MaxValue - 1);
            return delay <= TimeSpan.Zero ? TimeSpan.Zero : delay > max ? max : delay;
        }
    }

    internal static class Rand
    {
        public static double NextDouble()
        {
            Span<byte> bytes = stackalloc byte[8];
            RandomNumberGenerator.Fill(bytes);
            return (BitConverter.ToUInt64(bytes) >> 11) * (1.0 / (1UL << 53));
        }

        public static uint NextUInt32()
        {
            Span<byte> bytes = stackalloc byte[4];
            RandomNumberGenerator.Fill(bytes);
            return BitConverter.ToUInt32(bytes);
        }
    }
}
