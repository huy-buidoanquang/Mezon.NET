using System;
using System.Collections.Generic;
using System.Text;
using Grpc.Core;

namespace Mezon.Net.Core
{
    internal class RPCException : RpcException
    {
        public RPCException(Status status) : base(status)
        {
        }

        public RPCException(Status status, string message) : base(status, message)
        {
        }

        public RPCException(Status status, Metadata trailers) : base(status, trailers)
        {
        }

        public RPCException(Status status, Metadata trailers, string message) : base(status, trailers, message)
        {
        }
    }
}
