using Mezon.Net.Core.Abstractions;

namespace Mezon.Net.Transport
{
    public static class MezonNetworkTransporterExtensions
    {
        /// <summary>
        /// Discards any in-progress reassembly buffer for a chunked API (<c>MezonMessageType.Api</c>) response keyed by
        /// <paramref name="cid"/>.
        /// </summary>
        /// <param name="transporter">The active network transporter.</param>
        /// <param name="cid">Correlation id of the request whose partial API chunks should be dropped.</param>
        /// <remarks>
        /// <para>
        /// Large API responses may arrive as multiple wire frames. The transporter accumulates payload bytes in
        /// <c>_apiChunkBuffers</c> until the final chunk (finish flag <c>0xFF</c>) is received; only then is the
        /// assembled payload delivered via <see cref="IMezonNetworkTransporter.MessageReceived"/> and the buffer entry
        /// removed automatically.
        /// </para>
        /// <para><b>When to call</b></para>
        /// <list type="bullet">
        /// <item>
        /// <description>
        /// After a send-and-await operation fails or is abandoned (timeout, cancellation, RPC error, transport error)
        /// while the connection remains open and chunks for this <paramref name="cid"/> may already be on the wire or
        /// buffered.
        /// </description>
        /// </item>
        /// <item>
        /// <description>
        /// In <c>catch</c> / <c>finally</c> blocks of socket request helpers that allocate a <paramref name="cid"/>,
        /// send a message, and wait for a correlated reply (for example
        /// <c>SendSocketApiAsync</c>, <c>SendEnvelopeAsync</c>, heartbeat ping with wait).
        /// </description>
        /// </item>
        /// </list>
        /// <para><b>When not to call</b></para>
        /// <list type="bullet">
        /// <item><description>On the success path — the frame codec removes the entry when the last chunk arrives.</description></item>
        /// <item><description>After <see cref="IMezonNetworkTransporter.DisconnectAsync"/> — disconnect clears all chunk buffers.</description></item>
        /// <item><description>Before any chunk was received for <paramref name="cid"/> — the call is a harmless no-op.</description></item>
        /// </list>
        /// <para><b>Why it matters</b></para>
        /// <para>
        /// If a partial buffer is left behind, the same <paramref name="cid"/> reused by a later request can append to
        /// stale bytes or retain memory. This method prevents cross-request corruption; it does not cancel in-flight
        /// I/O and is unrelated to <c>SocketCorrelationHub</c> (which tracks application-level waiters, not wire
        /// reassembly).
        /// </para>
        /// <para>
        /// Only TCP and WebSocket Mezon transporters maintain API chunk buffers; other
        /// <see cref="IMezonNetworkTransporter"/> implementations are ignored.
        /// </para>
        /// </remarks>
        public static void RemoveApiChunkBuffer(this IMezonNetworkTransporter transporter, int cid)
        {
            switch (transporter)
            {
                case MezonNetworkTcpTransporter tcp:
                    tcp.RemoveApiChunkBuffer(cid);
                    break;
                case MezonNetworkWebSocketTransporter ws:
                    ws.RemoveApiChunkBuffer(cid);
                    break;
            }
        }
    }
}
