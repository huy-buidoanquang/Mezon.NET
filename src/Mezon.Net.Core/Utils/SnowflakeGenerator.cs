using System;
using System.Threading;

namespace Mezon.Net.Core
{
    /// <summary>
    ///     Provides a series of helper methods for handling and generating snowflake identifiers.
    ///     Implements the Mezon Snowflake algorithm for distributed ID generation.
    /// </summary>
    /// <remarks>
    ///     Mezon Snowflake ID structure (64 bits):
    ///     <list type="bullet">
    ///         <item>42 bits: Timestamp (milliseconds since Mezon epoch - Jan 1, 2020)</item>
    ///         <item>5 bits: Data Center ID (0-31)</item>
    ///         <item>5 bits: Worker ID (0-31)</item>
    ///         <item>12 bits: Sequence (0-4095)</item>
    ///     </list>
    ///     <para>
    ///     Bit layout: [timestamp:42][dataCenterId:5][workerId:5][sequence:12]
    ///     </para>
    ///     <para>
    ///     Shifts: timestamp << 22 | dataCenterId << 17 | workerId << 12 | sequence
    ///     </para>
    /// </remarks>
    public class SnowflakeGenerator
    {
        // Bit allocation constants (matching Mezon TypeScript implementation)
        private const int SequenceBits = 12;
        private const int WorkerIdBits = 5;
        private const int DataCenterIdBits = 5;
        // Timestamp gets remaining bits: 64 - 12 - 5 - 5 = 42 bits

        // Maximum values
        private const long MaxSequence = -1L ^ (-1L << SequenceBits);        // 4095
        private const long MaxWorkerId = -1L ^ (-1L << WorkerIdBits);        // 31
        private const long MaxDataCenterId = -1L ^ (-1L << DataCenterIdBits); // 31

        // Bit shifts (matching Mezon TypeScript: timestamp << 22 | dataCenterId << 17 | workerId << 12 | sequence)
        private const int SequenceShift = 0;
        private const int WorkerIdShift = SequenceBits;                      // 12
        private const int DataCenterIdShift = SequenceBits + WorkerIdBits;   // 17
        private const int TimestampLeftShift = SequenceBits + WorkerIdBits + DataCenterIdBits; // 22

        /// <summary>
        ///     Mezon epoch: January 1, 2020 00:00:00 UTC (1577836800000ms)
        /// </summary>
        /// <remarks>
        ///     This matches the epoch used in Mezon's TypeScript implementation.
        /// </remarks>
        private const ulong MezonEpoch = 1577836800000UL;

        private readonly long _workerId;
        private readonly long _dataCenterId;
        private readonly ulong _epoch;
        private readonly object _lock = new object();

        private long _lastTimestamp = -1L;
        private long _sequence = 0L;

        /// <summary>
        ///     Gets the worker ID for this generator instance.
        /// </summary>
        public long WorkerId => _workerId;

        /// <summary>
        ///     Gets the data center ID for this generator instance.
        /// </summary>
        public long DataCenterId => _dataCenterId;

        /// <summary>
        ///     Gets the epoch (start time in milliseconds) for this generator instance.
        /// </summary>
        public ulong Epoch => _epoch;

        /// <summary>
        ///     Initializes a new instance of the <see cref="SnowflakeGenerator"/> class.
        /// </summary>
        /// <param name="workerId">The worker ID (0-31). Defaults to 1.</param>
        /// <param name="dataCenterId">The data center ID (0-31). Defaults to 1.</param>
        /// <param name="epoch">The epoch timestamp in milliseconds. Defaults to Mezon epoch (Jan 1, 2020).</param>
        /// <exception cref="ArgumentException">Thrown when worker ID or data center ID is out of range.</exception>
        public SnowflakeGenerator(long workerId = 1, long dataCenterId = 1, ulong? epoch = null)
        {
            if (workerId < 0 || workerId > MaxWorkerId)
            {
                throw new ArgumentException($"Worker ID must be between 0 and {MaxWorkerId}", nameof(workerId));
            }

            if (dataCenterId < 0 || dataCenterId > MaxDataCenterId)
            {
                throw new ArgumentException($"Data Center ID must be between 0 and {MaxDataCenterId}", nameof(dataCenterId));
            }

            _workerId = workerId;
            _dataCenterId = dataCenterId;
            _epoch = epoch ?? MezonEpoch;
        }

        /// <summary>
        ///     Generates a new unique snowflake identifier.
        /// </summary>
        /// <returns>A unique <see cref="ulong"/> snowflake ID.</returns>
        /// <exception cref="InvalidOperationException">Thrown when clock moves backwards.</exception>
        /// <remarks>
        ///     This implementation matches Mezon's TypeScript generateSnowflakeId() function:
        ///     <code>
        ///     snowflakeId = ((timestamp - epoch) &lt;&lt; 22) | (dataCenterId &lt;&lt; 17) | (workerId &lt;&lt; 12) | sequence
        ///     </code>
        /// </remarks>
        public ulong Generate()
        {
            lock (_lock)
            {
                long timestamp = GetCurrentTimestamp();

                // Handle clock moving backwards
                if (timestamp < _lastTimestamp)
                {
                    throw new InvalidOperationException(
                        $"Clock moved backwards. Refusing to generate ID for {_lastTimestamp - timestamp}ms");
                }

                if (timestamp == _lastTimestamp)
                {
                    // Same millisecond, increment sequence
                    _sequence = (_sequence + 1) & MaxSequence;

                    if (_sequence == 0)
                    {
                        // Sequence overflow - wait for next millisecond
                        timestamp = WaitNextMillisecond(_lastTimestamp);
                    }
                }
                else
                {
                    // New millisecond, reset sequence to 0 (matching TS implementation)
                    _sequence = 0L;
                }

                _lastTimestamp = timestamp;

                // Compose the snowflake ID (matching Mezon TS implementation)
                // snowflakeId = ((timestamp - epoch) << 22) | (dataCenterId << 17) | (workerId << 12) | sequence
                ulong id = (ulong)((timestamp << TimestampLeftShift)
                    | (_dataCenterId << DataCenterIdShift)
                    | (_workerId << WorkerIdShift)
                    | _sequence);

                return id;
            }
        }

        /// <summary>
        ///     Resolves the timestamp from a snowflake identifier.
        /// </summary>
        /// <param name="value">The snowflake identifier to resolve.</param>
        /// <param name="epoch">Optional custom epoch. If null, uses the Mezon epoch (Jan 1, 2020).</param>
        /// <returns>
        ///     A <see cref="DateTimeOffset"/> representing when the snowflake was generated.
        /// </returns>
        public static DateTimeOffset FromSnowflake(ulong value, ulong? epoch = null)
        {
            ulong actualEpoch = epoch ?? MezonEpoch;
            long timestamp = (long)(value >> TimestampLeftShift);
            return DateTimeOffset.FromUnixTimeMilliseconds((long)(timestamp + (long)actualEpoch));
        }

        /// <summary>
        ///     Generates a pseudo-snowflake identifier from a <see cref="DateTimeOffset"/>.
        /// </summary>
        /// <param name="value">The time to be used in the snowflake.</param>
        /// <param name="epoch">Optional custom epoch. If null, uses the Mezon epoch (Jan 1, 2020).</param>
        /// <returns>
        ///     A <see cref="ulong"/> representing the pseudo-snowflake identifier.
        /// </returns>
        /// <remarks>
        ///     This generates a snowflake with zero worker ID, data center ID, and sequence number.
        ///     Only the timestamp component is set.
        /// </remarks>
        public static ulong ToSnowflake(DateTimeOffset value, ulong? epoch = null)
        {
            ulong actualEpoch = epoch ?? MezonEpoch;
            long timestamp = value.ToUnixTimeMilliseconds() - (long)actualEpoch;
            return (ulong)(timestamp << TimestampLeftShift);
        }

        /// <summary>
        ///     Extracts the worker ID from a snowflake identifier.
        /// </summary>
        /// <param name="value">The snowflake identifier.</param>
        /// <returns>The worker ID (0-31).</returns>
        public static long ExtractWorkerId(ulong value)
            => (long)((value >> WorkerIdShift) & MaxWorkerId);

        /// <summary>
        ///     Extracts the data center ID from a snowflake identifier.
        /// </summary>
        /// <param name="value">The snowflake identifier.</param>
        /// <returns>The data center ID (0-31).</returns>
        public static long ExtractDataCenterId(ulong value)
            => (long)((value >> DataCenterIdShift) & MaxDataCenterId);

        /// <summary>
        ///     Extracts the sequence number from a snowflake identifier.
        /// </summary>
        /// <param name="value">The snowflake identifier.</param>
        /// <returns>The sequence number (0-4095).</returns>
        public static long ExtractSequence(ulong value)
            => (long)(value & MaxSequence);

        /// <summary>
        ///     Parses all components from a snowflake identifier.
        /// </summary>
        /// <param name="value">The snowflake identifier.</param>
        /// <param name="epoch">Optional custom epoch. If null, uses the Mezon epoch (Jan 1, 2020).</param>
        /// <returns>
        ///     A tuple containing (Timestamp, DataCenterId, WorkerId, Sequence).
        /// </returns>
        public static (DateTimeOffset Timestamp, long DataCenterId, long WorkerId, long Sequence) Parse(
            ulong value,
            ulong? epoch = null)
        {
            return (
                FromSnowflake(value, epoch),
                ExtractDataCenterId(value),
                ExtractWorkerId(value),
                ExtractSequence(value)
            );
        }

        private long GetCurrentTimestamp()
        {
            return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds() - (long)_epoch;
        }

        private long WaitNextMillisecond(long lastTimestamp)
        {
            long timestamp = GetCurrentTimestamp();
            while (timestamp <= lastTimestamp)
            {
                Thread.Sleep(1);
                timestamp = GetCurrentTimestamp();
            }
            return timestamp;
        }
    }

    /// <summary>
    ///     Provides thread-safe access to a singleton snowflake generator instance.
    /// </summary>
    public static class SnowflakeGeneratorSingleton
    {
        private static readonly Lazy<SnowflakeGenerator> _instance =
            new Lazy<SnowflakeGenerator>(() => new SnowflakeGenerator());

        /// <summary>
        ///     Gets the singleton instance of the snowflake generator.
        /// </summary>
        public static SnowflakeGenerator Instance => _instance.Value;

        /// <summary>
        ///     Generates a new unique snowflake identifier using the singleton instance.
        /// </summary>
        /// <returns>A unique <see cref="ulong"/> snowflake ID.</returns>
        public static ulong Generate() => Instance.Generate();
    }
}
