
# Todos APi minimal API

## Features

- [x] Serve html for landing page
- [x] [Swagger](https://www.nuget.org/packages/Swashbuckle.AspNetCore/)
- [x] CRUD Api
- [x] [FluentValidation](https://fluentvalidation.net/)
- [x] TodosService based on List<T>
- [x] BasicAuthentication
- [x] Tests (XUnit)
- [ ] mapper(mapster or automaper)
- [ ] heroku deploy 




## Serve HTML for a landing page

In order to serve html files from the ```wwwroot``` folder add the followings:
- create a folder ``` wwwroot ``` in the root of the project
- inside ```program.cs``` add the following line so that html files can be served to the root url of the api

```c#
app.UseFileServer();
```

Once the up items are present the ```index.html``` from the ```wwwroot``` folder will be served

## Swagger
In order to have OpenApi exposed for definitions and testing use the following package
[Swashbuckle](https://www.nuget.org/packages/Swashbuckle.AspNetCore/) : 

```
dotnet add package Swashbuckle.AspNetCore --version 6.2.3
 ```

Once the package is added update ```Program.cs``` with the following before line ```var app = builder.Build();```

```c#
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = builder.Environment.ApplicationName, Version = "v1" });
});
```

In order to use it in the browser we have to allign  with the ```app``` so add the following lines after ```var app = builder.Build();```:

```c#
app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{builder.Environment.ApplicationName} v1"));
```

Ok, now if we restart the api and navigate to ```...../swagger``` we will be presented with the OpenApi definitions.


## CRUD Api implemented
- [x] Create a todo
- [x] Get all todos
- [x] Get by Id
- [x] Update todo
- [x] Delete todo

## Fluent Validation

Validation of data received from the front end is based on [FluentValidation](https://fluentvalidation.net/).
Here is an example of model validation for create a new todo:

```c#
public class Validator : AbstractValidator<TodoDto>
{
    public Validator() => RuleFor(x => x.Title).NotNull().WithMessage("Title required");
}
```
For the validation only the Title is checked and one way to do validation inside of the api actions is this one:

```c#
var validationResult = validator.Validate(dto);
if (!validationResult.IsValid)
  return Results.BadRequest(validationResult.Errors);        
```

## Basic authentication

Basic authentication will allow the creation of a logged user based on the username sent via the sent Authorization header.

The implementation is based on ```AuthenticationHandler``` that is added via ```builder.Services.AddAuthentication("BasicAuthentication")``` .

In order to work with a real user/password system the underlying logic must be created. In the same is just as an example without validation.

On the actions that require authentication/authorization the following parameter must be added: ```[Authorize] ```.

```c#
app.MapGet("/api/todos", [Authorize] (TodosService todosService, ClaimsPrincipal user) =>{});
```

The swagger configuration was updated so that it will provide Basic Authentication integration.

