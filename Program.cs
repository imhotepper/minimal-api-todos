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
    return todo != null ? (Task)Results.Ok(todo) : (Task)Results.NotFound();
});

//Create
app.MapPost("/api/todos",
    (TodoDto dto, TodosService todosService, ILogger<Todo> logger, IValidator<TodoDto> validator) =>
    {
        logger.LogInformation($"Receiced POST request: {JsonSerializer.Serialize(dto)}");
        var validationResult = validator.Validate(dto);

        if (!validationResult.IsValid)
        {
            logger.LogError($"Invalid request received:{JsonSerializer.Serialize(dto)} ");
            logger.LogError($"Errors: {JsonSerializer.Serialize(validationResult.Errors)} ");
            return Results.BadRequest(validationResult.Errors);
        }

        var id = todosService.Create(dto);
        logger.LogInformation($"Created new todo with id: {id}");
        return Results.Created($"/api/todos/{id}", null);
    });

//Update
app.MapPut("/api/todos/{id}",
    (int id, TodoDto dto, TodosService todosService, ILogger<Todo> logger, IValidator<TodoDto> validator) =>
    {
        logger.LogInformation($"Receiced UPDATE request: {JsonSerializer.Serialize(dto)}");
        
        var validationResult = validator.Validate(dto);
        if (!validationResult.IsValid)
        {
            logger.LogError($"Invalid request received:{JsonSerializer.Serialize(dto)} \n\n" + 
                            $" {JsonSerializer.Serialize(validationResult.Errors)} ");
            return Results.BadRequest(validationResult.Errors);
        }

        todosService.Update(id, dto);
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

public record Todo(int? Id, string Title, bool IsCompleted, DateTime doc);

//  public class Validator: AbstractValidator<Todo>{
//         public Validator()
//         {
//             RuleFor(x => x.Title).NotNull().WithMessage("Title required");
//         }
//     }

public record TodoDto(int? Id, [Required] [MinLength(3)] String Title, bool IsCompleted);

public class Validator : AbstractValidator<TodoDto>
{
    public Validator() => RuleFor(x => x.Title).NotNull().WithMessage("Title required");
}


public class TodosService
{
    //Target Typed new
    private static List<Todo> _todos = new List<Todo>();

    public int Create(TodoDto dto)
    {
        var todo = new Todo(_todos.Count + 1, dto.Title, dto.IsCompleted, DateTime.Now);
        _todos.Add(todo);
        return todo.Id ?? -1;
    }

    public IEnumerable<Todo> GetAll() => _todos.ToList<Todo>();

    public bool Delete(int id) => _todos.Remove(_todos.FirstOrDefault(x => x.Id == id));

    public void Update(int id, TodoDto dto)
    {
        var todo = _todos.FirstOrDefault(x => x.Id == id);
        if (todo == null) return;
        var newTodo = todo with { Title = dto.Title, IsCompleted = dto.IsCompleted };
        _todos.Remove(todo);
        _todos.Add(newTodo);
    }

    public Todo GetById(int id) => _todos.FirstOrDefault(x => x.Id == id);
}