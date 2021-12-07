using System;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Builder;
using MiniValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

//swagger registration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = builder.Environment.ApplicationName, Version = "v1" });
});

builder.Services.AddScoped<TodosService>();

// builder.Logging.AddJsonConsole();
builder.Logging.AddConsole();
var app = builder.Build();

// enable static file
app.UseFileServer();

//swagger initialization
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{builder.Environment.ApplicationName} v1"));

//GetAll

app.MapGet("/api/todos", (TodosService todosService) => Results.Ok(todosService.GetAll()));

//GetById

//Create
app.MapPost("/api/todos", (TodoDto dto, TodosService todosService, ILogger<Todo> logger) =>
 {
     logger.LogInformation("Receiced request: ");
     if (!MiniValidator.TryValidate(dto, out var errors))
     {
         logger.LogWarning($"Bad request with data: {errors}");
         return Results.BadRequest(errors);
     }
     int id = todosService.Create(dto);
     logger.LogInformation($"Created new todo with id: {id}");
     return Results.Created($"/api/todos/{id}", null);
 });

//Update

//Delete

app.MapGet("/api/ping", () => "pong!");

app.Run();



public record Todo(int? Id, String Title, bool IsCompleted, DateTime doc);

public record TodoDto(int? Id, [Required] String Title, bool IsCompleted);

public class TodosService
{
    private static List<Todo> _todos = new List<Todo>();
    public int Create(TodoDto dto)
    {
        var todo = new Todo(_todos.Count + 1, dto.Title, dto.IsCompleted, DateTime.Now);
        _todos.Add(todo);
        return todo.Id ?? -1;
    }

    public IEnumerable<Todo> GetAll() => _todos.ToList<Todo>();
}
