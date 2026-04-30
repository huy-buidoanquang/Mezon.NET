using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Mezon.Net.WebSocket")]

namespace Mezon.Net.Api
{
    public static class AssemblyReference
    {
        public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
    }
}
