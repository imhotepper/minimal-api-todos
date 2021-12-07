var builder = WebApplication.CreateBuilder(args);

//swagger registration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = builder.Environment.ApplicationName, Version = "v1" });
});

 builder.Services.AddScoped<TodosService>();
var app = builder.Build();
 
// enable static file
app.UseFileServer();

//swagger initialization
 app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{builder.Environment.ApplicationName} v1"));

//GetAll

//GetById

//Create
app.MapPost("/api/todos",(TodoDto dto, TodosService service)=>{
    
});

//Update

//Delete

app.MapGet("/api/ping", () => "pong!");

app.Run();



public record Todo(int Id, String Title, bool IsCompleted, DateTime doc);

public record TodoDto(int Id, String Title, bool IsCompleted);

public class TodosService{}
