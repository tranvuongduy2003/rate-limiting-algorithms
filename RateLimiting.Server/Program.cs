using RateLimiting.Server.Features.FixedWindowCounter;
using RateLimiting.Server.Features.LeakingBucket;
using RateLimiting.Server.Features.SlidingWindowCounter;
using RateLimiting.Server.Features.SlidingWindowLog;
using RateLimiting.Server.Features.TokenBucket;

var builder = WebApplication.CreateBuilder(args);

builder.AddServiceDefaults();

builder.Services.AddProblemDetails();
builder.Services.AddSingleton(TimeProvider.System);

builder.Services
    .AddTokenBucket(builder.Configuration)
    .AddLeakingBucket(builder.Configuration)
    .AddFixedWindowCounter(builder.Configuration)
    .AddSlidingWindowLog(builder.Configuration)
    .AddSlidingWindowCounter(builder.Configuration);

builder.Services.AddOpenApi();

var app = builder.Build();

app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.MapGroup("/api")
    .MapTokenBucket()
    .MapLeakingBucket()
    .MapFixedWindowCounter()
    .MapSlidingWindowLog()
    .MapSlidingWindowCounter();

app.MapDefaultEndpoints();

app.UseFileServer();

app.Run();
