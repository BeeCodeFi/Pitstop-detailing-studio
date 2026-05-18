namespace DataEntry.Api.Services;

public interface IExplorerModeAccessor
{
    bool IsExplorer { get; set; }
}

/// <summary>
/// Scoped service that tracks whether the current HTTP request is made by an Explorer-role user.
/// Set by ExplorerModeMiddleware after authentication resolves the JWT claims.
/// AppDbContext reads this to skip SaveChanges, making all writes a no-op for Explorer users.
/// </summary>
public class ExplorerModeAccessor : IExplorerModeAccessor
{
    public bool IsExplorer { get; set; }
}
