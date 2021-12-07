var builder = WebApplication.CreateBuilder(args);
// builder.WebHost.UseWebRoot("webroot");

var app = builder.Build();

app.UseFileServer();

//app.UseStaticFiles();

app.MapGet("/api/ping", () => "pong!");

app.Run();
