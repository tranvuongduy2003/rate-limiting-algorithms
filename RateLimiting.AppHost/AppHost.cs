var builder = DistributedApplication.CreateBuilder(args);

var redis = builder.AddRedis("redis");

var server = builder.AddProject<Projects.RateLimiting_Server>("server")
    .WithReference(redis)
    .WaitFor(redis)
    .WithHttpHealthCheck("/health")
    .WithExternalHttpEndpoints();

var webfrontend = builder.AddViteApp("webfrontend", "../frontend")
    .WithReference(server)
    .WaitFor(server);

server.PublishWithContainerFiles(webfrontend, "wwwroot");

builder.Build().Run();
