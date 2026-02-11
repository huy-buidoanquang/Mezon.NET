using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Mezon.NET.WebSocket")]

namespace Mezon.NET.Api
{
    public static class AssemblyReference
    {
        public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
    }
}
