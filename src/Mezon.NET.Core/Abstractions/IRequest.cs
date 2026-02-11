using System;
using Mezon.NET.Core;

namespace Mezon.NET.Abstractions
{
    public interface IRequest
    {
        DateTimeOffset? TimeoutAt { get; }
        RequestOptions Options { get; }
    }
}
