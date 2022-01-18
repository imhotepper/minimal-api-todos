
# Todos APi minimal API


## [Presentation](https://slides.com/imhotepp/minimal-api/fullscreen)


## Features

- [x] Serve html for landing page
- [x] [Swagger](https://www.nuget.org/packages/Swashbuckle.AspNetCore/)
- [x] CRUD Api
- [x] [FluentValidation](https://fluentvalidation.net/) && [MiniValidation](https://github.com/DamianEdwards/MiniValidation)
- [x] TodosService based on List<T>
- [x] Global error handling
- [x] BasicAuthentication
- [x] Integration Tests [XUnit](https://xunit.net/)
- [x] [Automaper](https://automapper.org/)
- [x] azure/heroku deploy 


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


##  Global error handling

For global error handling we will use the provided error handler middleware.

In order to show error details while in developemnt then we could use Develper exception page ```app.UseDeveloperExceptionPage()``` . 
In order to add a custom response while not in developement then the ```app.UseExceptionHandler(...)``` can be added and customize the response. The Exception will be logged by the handler automaticaly so no need to re log it. 

```c#

if (app.Environment.IsDevelopment())
    app.UseDeveloperExceptionPage();
else
// custom global error handler
app.UseExceptionHandler((errorApp) =>
{
    errorApp.Run(async (context) =>
    {
     //Custom code!
    });
});
}

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

## Testing the api

In order to test the api a new project was added with the xUnit dependencies.

The tests are linked to the api endpoints and they reflect validation on: Read, Create, Update and Delete.


## Automapper

Automapper: A convention-based object-object mapper. The mapper will do the translation form the TodoDto to the Todo model and the other way arround.


## Deploy

The api is available on :
- [Heroku](https://miniapi1.herokuapp.com/)
- [Azure](https://miniapi1.azurewebsites.net/)

The deployment is done via Actions from Github to Azure and via standard git deploy with .net buildpack for Heroku.

<br><br><br>
Happy coding! 

D!
