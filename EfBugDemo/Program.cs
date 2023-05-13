using EfBugDemo;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Npgsql;

var builder = WebApplication.CreateBuilder(args);

NpgsqlConnectionStringBuilder connectionConfig =
    new NpgsqlConnectionStringBuilder(builder.Configuration["PostgresDatabase"]);

builder.Services.AddSingleton<DbContextTracker>();
builder.Services.AddDbContext<MyDbContext>(options =>
    {
        NpgsqlDataSourceBuilder npgBuilder = new NpgsqlDataSourceBuilder(connectionConfig.ConnectionString);

        options.UseNpgsql(npgBuilder.Build())
            .ConfigureWarnings(warnings => warnings.Ignore(CoreEventId.ManyServiceProvidersCreatedWarning));
    }
);

var app = builder.Build();
await app.Services.CheckContextTracker<MyDbContext>();


app.MapGet("/", () => "Hello World!");

app.Run();


public class MyDbContext : DbContext
{
    public MyDbContext(DbContextOptions<MyDbContext> options, DbContextTracker tracker) : base(options)
    {
        tracker.AddContext(this);
    }
}