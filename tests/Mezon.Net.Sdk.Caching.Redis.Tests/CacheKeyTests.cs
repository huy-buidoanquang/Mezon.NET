using Mezon.Net.Sdk.Caching;
using Xunit;

namespace Mezon.Net.Sdk.Caching.Redis.Tests;

public sealed class CacheKeyTests
{
    [Fact]
    public void ToRedisKey_formats_expected_segments()
    {
        var key = new CacheKey("prod", 42, "channel", "9001");
        Assert.Equal("prod:42:channel:9001", key.ToRedisKey());
    }

    [Fact]
    public void Parse_round_trips_ToRedisKey()
    {
        var original = new CacheKey("staging", 7, "clan", "abc-123");
        var parsed = CacheKey.Parse(original.ToRedisKey());
        Assert.Equal(original, parsed);
    }

    [Theory]
    [InlineData("bad")]
    [InlineData("a:b")]
    [InlineData("a:b:c")]
    public void Parse_throws_for_malformed_keys(string value)
    {
        Assert.Throws<FormatException>(() => CacheKey.Parse(value));
    }

    [Fact]
    public void Constructor_rejects_colon_in_segments()
    {
        Assert.Throws<ArgumentException>(() => new CacheKey("prod:evil", 1, "channel", "1"));
    }
}
