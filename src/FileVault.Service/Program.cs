// src/FileVault.Service/Program.cs
using FileVault.Service;

var builder = Host.CreateApplicationBuilder(args);
builder.Services.AddWindowsService(options =>
    options.ServiceName = "FileVault Service");
builder.Services.AddHostedService<Worker>();

var host = builder.Build();
host.Run();
