#if !NET5_0_OR_GREATER
namespace System.Runtime.CompilerServices
{
    /// <summary>Polyfill for init-only setters on older TFMs.</summary>
    internal static class IsExternalInit
    {
    }
}
#endif
