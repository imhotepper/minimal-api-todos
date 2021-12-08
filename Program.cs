using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Builder;
using MiniValidation;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.OpenApi.Models;
using System.Text.Json;
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

//Create
app.MapPost("/api/todos", (TodoDto dto, TodosService todosService, ILogger<Todo> logger /*, IValidator<TodoDto> validator*/) =>
 {
// ValidationResult validationResult = validator.Validate(dto);

//     if (!validationResult.IsValid)
//     {
//         //return Results.BadRequest(validationResult);
//         foreach(var failure in validationResult.Errors)
//   {
  //  Console.WriteLine("Property " + failure.PropertyName + " failed validation. Error was: " + failure.ErrorMessage);
 

     logger.LogInformation($"Receiced request: {JsonSerializer.Serialize(dto)}" );
     logger.LogInformation($"Is model valid => {MiniValidator.TryValidate(dto, out var errors1)}");
     logger.LogInformation($"Is model valid => {JsonSerializer.Serialize(errors1)}");
     
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



public record  Todo(int? Id, String Title, bool IsCompleted, DateTime doc){
    public class Validator: AbstractValidator<Todo>{
        public Validator()
        {
            RuleFor(x => x.Title).NotNull().WithMessage("Title required");
        }
    }
}

public record TodoDto(int? Id, [Required][MinLength(3)] String Title, bool IsCompleted){
     public class Validator: AbstractValidator<Todo>{
        public Validator()
        {
            RuleFor(x => x.Title).NotNull().WithMessage("Title required");
        }
    }
}

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
