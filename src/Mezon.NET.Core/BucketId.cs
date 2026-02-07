using System;
using System.Collections.Generic;
using System.Linq;

namespace Mezon.NET.Core
{
    /// <summary>
    ///     Represents a ratelimit bucket.
    /// </summary>
    public class BucketId : IEquatable<BucketId>
    {
        /// <summary>
        ///     Gets the http method used to make the request if available.
        /// </summary>
        public string HttpMethod { get; }
        /// <summary>
        ///     Gets the endpoint that is going to be requested if available.
        /// </summary>
        public string Endpoint { get; }
        /// <summary>
        ///     Gets the major parameters of the route.
        /// </summary>
        public IOrderedEnumerable<KeyValuePair<string, string>> MajorParameters { get; }
        /// <summary>
        ///     Gets the hash of this bucket.
        /// </summary>
        /// <remarks>
        ///     The hash is provided by Discord to group ratelimits.
        /// </remarks>
        public string BucketHash { get; }
        /// <summary>
        ///     Gets if this bucket is a hash type.
        /// </summary>
        public bool IsHashBucket { get => BucketHash != null; }

        private BucketId(string httpMethod, string endpoint, IEnumerable<KeyValuePair<string, string>> majorParameters, string bucketHash)
        {
            HttpMethod = httpMethod;
            Endpoint = endpoint;
            MajorParameters = majorParameters.OrderBy(x => x.Key);
            BucketHash = bucketHash;
        }

        /// <summary>
        ///     Creates a new <see cref="BucketId"/> based on the
        ///     <see cref="HttpMethod"/> and <see cref="Endpoint"/>.
        /// </summary>
        /// <param name="httpMethod">Http method used to make the request.</param>
        /// <param name="endpoint">Endpoint that is going to receive requests.</param>
        /// <param name="majorParams">Major parameters of the route of this endpoint.</param>
        /// <returns>
        ///     A <see cref="BucketId"/> based on the <see cref="HttpMethod"/>
        ///     and the <see cref="Endpoint"/> with the provided data.
        /// </returns>
        public static BucketId Create(string httpMethod, string endpoint, Dictionary<string, string> majorParams)
        {
            Check.NotNullOrWhitespace(endpoint, nameof(endpoint));
            majorParams ??= new Dictionary<string, string>();
            return new BucketId(httpMethod, endpoint, majorParams, string.Empty);
        }

        /// <summary>
        ///     Creates a new <see cref="BucketId"/> based on a
        ///     <see cref="BucketHash"/> and a previous <see cref="BucketId"/>.
        /// </summary>
        /// <param name="hash">Bucket hash provided by Discord.</param>
        /// <param name="oldBucket"><see cref="BucketId"/> that is going to be upgraded to a hash type.</param>
        /// <returns>
        ///     A <see cref="BucketId"/> based on the <see cref="BucketHash"/>
        ///     and <see cref="MajorParameters"/>.
        /// </returns>
        public static BucketId Create(string hash, BucketId oldBucket)
        {
            Check.NotNullOrWhitespace(hash, nameof(hash));
            Check.NotNull(oldBucket, nameof(oldBucket));
            return new BucketId(string.Empty, string.Empty, oldBucket.MajorParameters, hash);
        }

        /// <summary>
        ///     Gets the string that will define this bucket as a hash based one.
        /// </summary>
        /// <returns>
        ///     A <see cref="string"/> that defines this bucket as a hash based one.
        /// </returns>
        public string GetBucketHash()
            => IsHashBucket ? $"{BucketHash}:{string.Join("/", MajorParameters.Select(x => x.Value))}" : string.Empty;

        /// <summary>
        ///     Gets the string that will define this bucket as an endpoint based one.
        /// </summary>
        /// <returns>
        ///     A <see cref="string"/> that defines this bucket as an endpoint based one.
        /// </returns>
        public string GetUniqueEndpoint()
            => HttpMethod != null ? $"{HttpMethod} {Endpoint}" : Endpoint;

        public override bool Equals(object? obj)
        {
            if (obj is null)
            {
                return false;
            }

            return Equals(obj);
        }

        public override int GetHashCode()
            => IsHashBucket ? (BucketHash, string.Join("/", MajorParameters.Select(x => x.Value))).GetHashCode() : (HttpMethod, Endpoint).GetHashCode();

        public override string ToString()
            => GetBucketHash() ?? GetUniqueEndpoint();

        public bool Equals(BucketId? other)
        {
            if (other is null)
            {
                return false;
            }

            if (ReferenceEquals(this, other))
            {
                return true;
            }

            if (GetType() != other.GetType())
            {
                return false;
            }

            return ToString() == other.ToString();
        }
    }

    internal class BucketIds
    {
        public ulong GuildId { get; internal set; }
        public ulong ChannelId { get; internal set; }
        public ulong WebhookId { get; internal set; }
        public string? HttpMethod { get; internal set; }

        internal BucketIds(ulong guildId = 0, ulong channelId = 0, ulong webhookId = 0, string? httpMethod = null)
        {
            GuildId = guildId;
            ChannelId = channelId;
            WebhookId = webhookId;
            HttpMethod = httpMethod;
        }

        internal object[] ToArray()
            => new object[] { HttpMethod ?? string.Empty, GuildId, ChannelId, WebhookId };

        internal Dictionary<string, string> ToMajorParametersDictionary()
        {
            var dict = new Dictionary<string, string>();
            if (GuildId != 0)
            {
                dict["GuildId"] = GuildId.ToString();
            }

            if (ChannelId != 0)
            {
                dict["ChannelId"] = ChannelId.ToString();
            }

            if (WebhookId != 0)
            {
                dict["WebhookId"] = WebhookId.ToString();
            }

            return dict;
        }

        internal static int? GetIndex(string name)
        {
            return name switch
            {
                "httpMethod" => 0,
                "guildId" => 1,
                "channelId" => 2,
                "webhookId" => 3,
                _ => null,
            };
        }
    }
}
