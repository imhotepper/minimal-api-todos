using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.OpenApi.Models;
using System.Text.Json;
using System.Threading.Tasks;
using FluentValidation;
using Microsoft.Extensions.Logging;
using MiniValidation;


var builder = WebApplication.CreateBuilder(args);
//FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Todo>(lifetime: ServiceLifetime.Scoped);
//swagger registration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = builder.Environment.ApplicationName, Version = "v1" });
});
//add services used by the api
builder.Services.AddScoped<TodosService>();
// builder.Logging.AddJsonConsole();
builder.Logging.AddConsole();

var app = builder.Build();

// enable static file
app.UseFileServer();

// http logging
//app.UseHttpLogging();

//swagger initialization
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{builder.Environment.ApplicationName} v1"));

//GetAll
app.MapGet("/api/todos", (TodosService todosService) => Results.Ok(todosService.GetAll()));

//GetById
app.MapGet("/api/todos/{id}", (int id, TodosService todosService, ILogger<Todo> logger) =>
{
    logger.LogInformation($"Receiced get request for Id: {id}");
    var todo = todosService.GetById(id);
    return todo != null ? Results.Ok(todo) : Results.NotFound();
});

//Create
app.MapPost("/api/todos",
    (Todo todo, TodosService todosService, ILogger<Todo> logger, IValidator<Todo> validator) =>
    {
        logger.LogInformation($"Receiced POST request: {JsonSerializer.Serialize(todo)}");
        
        var validationResult = validator.Validate(todo);

        if (!validationResult.IsValid)
        {
            logger.LogError($"Invalid request received:{JsonSerializer.Serialize(todo)} ");
            logger.LogError($"Errors: {JsonSerializer.Serialize(validationResult.Errors)} ");
            return Results.BadRequest(
                validationResult.Errors
                    .Select(x => new { Property = x.PropertyName, Error = x.ErrorMessage}));
        }
        
        var id = todosService.Create(todo);
        logger.LogInformation($"Created new todo with id: {id}");
        return Results.Created($"/api/todos/{id}", null);
    });

//Update
app.MapPut("/api/todos/{id}",
    (int id, Todo todo, TodosService todosService, ILogger<Todo> logger, IValidator<Todo> validator) =>
    {
        logger.LogInformation($"Receiced UPDATE request: {JsonSerializer.Serialize(todo)}");

        var validationResult = validator.Validate(todo);
        if (!validationResult.IsValid)
        {
            logger.LogError($"Invalid request received:{JsonSerializer.Serialize(todo)} \n\n" +
                            $" {JsonSerializer.Serialize(validationResult.Errors)} ");
            return Results.BadRequest(
                validationResult.Errors
                    .Select(x => new { Property = x.PropertyName, Error = x.ErrorMessage}));
        }

        todosService.Update(id, todo);
        logger.LogInformation($"Update  todo with id: {id}");
        return Results.Accepted();
    });

//Delete
app.MapDelete("/api/todos/{id}",
    (int id, TodosService todosService, ILogger<Todo> logger) =>
    {
        return todosService.Delete(id)
            ? (Task)Results.NoContent()
            : (Task)Results.StatusCode((int)HttpStatusCode.InternalServerError);
    });

app.MapGet("/api/ping", () => "pong!");

app.Run();


//Todos Service

public record Todo(int? Id, string Title, bool IsCompleted);

public class Validator : AbstractValidator<Todo>
{
    public Validator() => RuleFor(x => x.Title).NotNull().WithMessage("Title required");
}


public class TodosService
{
    //Target Typed new
    private static List<Todo> _todos = new List<Todo>();

    public int Create(Todo todo)
    {
        todo = todo with { Id = _todos.Count + 1 };
        _todos.Add(todo);
        return todo.Id ?? -1;
    }

    public IEnumerable<Todo> GetAll() => _todos.ToList<Todo>();

    public bool Delete(int id) => _todos.Remove(_todos.FirstOrDefault(x => x.Id == id));

    public void Update(int id, Todo todo)
    {
        var td = _todos.FirstOrDefault(x => x.Id == id);
        if (td == null) return;
        var newTodo = td with { Title = todo.Title, IsCompleted = todo.IsCompleted }; 
        _todos.Remove(td);
        _todos.Add(newTodo);
    }

    public Todo GetById(int id) => _todos.FirstOrDefault(x => x.Id == id);
}