using System.Collections.Concurrent;
using Microsoft.EntityFrameworkCore;

namespace EfBugDemo;

public class DbContextUsage
{
    public int Total { get; set; }
    public int NotDisposed { get; set; }
    
    public override string ToString()
    {
        return $"Total: {Total}, NotDisposed: {NotDisposed}";
    }
}

/// <summary>
/// Stores all connection in a concurrent bag
/// </summary>
public class DbContextTracker
{

    private readonly ILogger _logger;
    private readonly ConcurrentBag<DbContext> _contexts = new();
    
    public DbContextTracker(ILogger<DbContextTracker> logger)
    {
        _logger = logger;
    }

    public void AddContext(DbContext context)
    {
        _contexts.Add(context);
    }

    public void ReportUsage()
    {
        _logger.LogInformation("DbContext usage: {Usage}", GetUsage());
    }

    public DbContextUsage GetUsage()
    {
        var usage = new DbContextUsage();
        foreach (var context in _contexts)
        {
            usage.Total++;
            
            if (!IsDisposed(context))
            {
                usage.NotDisposed++;
            }
        }

        return usage;
    }
    
    bool IsDisposed(DbContext context)
    {
        try
        {
            context.Database.ExecuteSqlRaw("SELECT 1");
            return false;
        }
        catch (ObjectDisposedException)
        {
            return true;
        }
    }
}

public static class DbContextTrackerExtensions
{
    public static async Task CheckContextTracker<T>(this IServiceProvider serviceProvider)
        where T : DbContext
    {
        serviceProvider.GetRequiredService<IServiceScopeFactory>();
        var tracker = serviceProvider.GetRequiredService<DbContextTracker>();

        for (int i = 0; i < 10000; i++)
        {
            await RunBatch<T>(serviceProvider, 20);
            tracker.ReportUsage();
        }
    }
    
    static async Task RunBatch<T>(IServiceProvider serviceProvider, int batchSize)
        where T : DbContext
    {
        List<IDisposable> scopes = new();
        List<Task> tasks = new();
        
        for (int i = 0; i < batchSize; i++)
        {
            var scope = serviceProvider.CreateScope();
            scopes.Add(scope);
            
            
            var context = scope.ServiceProvider.GetRequiredService<T>();
            
            tasks.Add(context.Database.ExecuteSqlRawAsync("SELECT 1"));
        }
        
        await Task.WhenAll(tasks);
        
        // Dispose
        foreach (var scope in scopes)
        {
            scope.Dispose();
        }
    }
}