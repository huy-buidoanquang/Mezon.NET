namespace Mezon.Net.Client
{
    /// <summary>
    /// How realtime events are handed to subscribers.
    /// </summary>
    public enum EventDispatchMode
    {
        /// <summary>
        /// Events for the same channel (or clan, for events without a channel) are delivered one at a time in the order
        /// they were received, using a fixed number of bounded lanes. A handler that runs longer than
        /// <see cref="MezonSocketClientOptions.SocketHandlerTimeoutInMilliseconds"/> stops holding its lane. When a lane
        /// is full, new events for it are dropped and counted rather than blocking the socket.
        /// </summary>
        Ordered = 0,

        /// <summary>
        /// Every event is dispatched independently on the thread pool, without ordering or a backlog bound
        /// (the behaviour before 1.7.0).
        /// </summary>
        Concurrent = 1,
    }
}
