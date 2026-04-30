using System;
using Mezon.Net.Core;

namespace Mezon.Net.Abstractions
{
    public interface IRequest
    {
        DateTimeOffset? TimeoutAt { get; }
        RequestOptions Options { get; }
    }
}
