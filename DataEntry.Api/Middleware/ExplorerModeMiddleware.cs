using DataEntry.Api.Services;

namespace DataEntry.Api.Middleware;

/// <summary>
/// Must be placed AFTER UseAuthentication() so that User.IsInRole() is available.
/// Sets IExplorerModeAccessor.IsExplorer = true for any authenticated Explorer-role user,
/// causing AppDbContext to skip all SaveChanges calls for the lifetime of that request.
/// </summary>
public class ExplorerModeMiddleware
{
    private readonly RequestDelegate _next;

    public ExplorerModeMiddleware(RequestDelegate next)
    {
        _next = next;
    }

    public async Task InvokeAsync(HttpContext context, IExplorerModeAccessor explorerMode)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            explorerMode.IsExplorer = context.User.IsInRole("Explorer");
        }

        await _next(context);
    }
}
