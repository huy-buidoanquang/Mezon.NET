using System.Threading.Tasks;

namespace Mezon.Net.Sdk.Commands
{
    public delegate Task CommandMiddlewareDelegate(ICommandContext context);
    public delegate Task CommandMiddleware(ICommandContext context, CommandMiddlewareDelegate next);
}
