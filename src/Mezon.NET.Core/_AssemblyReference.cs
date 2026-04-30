using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Mezon.Net.Api")]
[assembly: InternalsVisibleTo("Mezon.Net.WebSocket")]
[assembly: InternalsVisibleTo("Mezon.Net.Tests")]
[assembly: InternalsVisibleTo("Mezon.Net.Transport")]


namespace Mezon.Net.Core
{
    public static class AssemblyReference
    {
        public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
    }
}
