using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Mezon.NET.Api")]
[assembly: InternalsVisibleTo("Mezon.NET.WebSocket")]
[assembly: InternalsVisibleTo("Mezon.NET.Tests")]


namespace Mezon.NET.Core
{
    public static class AssemblyReference
    {
        public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
    }
}