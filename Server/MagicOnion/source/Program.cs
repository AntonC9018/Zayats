using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.AspNetCore.Hosting;

var builder = WebApplication.CreateBuilder(args);

// Additional configuration is required to successfully run gRPC on macOS.
// For instructions on how to configure Kestrel and gRPC clients on macOS, visit https://go.microsoft.com/fwlink/?linkid=2099682

{
    builder.WebHost.UseUrls();
    
    var services = builder.Services;
    services.AddGrpc();
    services.AddMagicOnion();
}

var app = builder.Build();

{
    app.UseRouting();
    app.UseEndpoints(endpoints =>
    {
        endpoints.MapMagicOnionService();
        endpoints.MapGet("/", async context =>
        {
            await context.Response.WriteAsync("Communication with gRPC endpoints must be made through a gRPC client. To learn how to create a client, visit: https://go.microsoft.com/fwlink/?linkid=2086909");
        });
    });
}

app.Run();
