using System;

namespace Mezon.NET.Core.Abstractions
{
    public interface IRequest
    {
        DateTimeOffset? TimeoutAt { get; }
        RequestOptions Options { get; }
    }
}
