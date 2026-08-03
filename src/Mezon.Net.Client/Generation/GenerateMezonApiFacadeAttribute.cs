using System;

namespace Mezon.Net.Client.Generation
{
    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    internal sealed class GenerateMezonAuthApiFacadeAttribute : Attribute
    {
    }

    [AttributeUsage(AttributeTargets.Class, AllowMultiple = false)]
    internal sealed class GenerateMezonSocketApiFacadeAttribute : Attribute
    {
    }
}
