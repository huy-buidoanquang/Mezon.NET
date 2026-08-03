using System.Reflection;
using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("Mezon.Net.Client")]

namespace Mezon.Net.Client
{
    public static class AssemblyReference
    {
        public static readonly Assembly Assembly = typeof(AssemblyReference).Assembly;
    }
}
