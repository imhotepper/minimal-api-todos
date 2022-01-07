using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.OpenApi.Models;
using System.Text.Json;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;


var builder = WebApplication.CreateBuilder(args);

//FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Todo>(lifetime: ServiceLifetime.Scoped);

//swagger registration
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = builder.Environment.ApplicationName, Version = "v1" });
    c .AddSecurityDefinition("basic", new OpenApiSecurityScheme  
    {  
        Name = "Authorization",  
        Type = SecuritySchemeType.Http,  
        Scheme = "basic",  
        In = ParameterLocation.Header,  
        Description = "Basic Authorization header using the Bearer scheme."  
    });  
    c.AddSecurityRequirement(new OpenApiSecurityRequirement  
    {  
        {  
            new OpenApiSecurityScheme  
            {  
                Reference = new OpenApiReference  
                {  
                    Type = ReferenceType.SecurityScheme,  
                    Id = "basic"  
                }  
            },
            Array.Empty<string>()
        }  
    });
});
//add services used by the api
builder.Services.AddScoped<TodosService>();
builder.Services.AddScoped<UserService>();
// builder.Logging.AddJsonConsole();
builder.Logging.AddConsole();


builder.Services.AddAuthentication("BasicAuthentication")
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);

builder.Services.AddAuthorization();

var app = builder.Build();

// enable static file
app.UseFileServer();

// http logging
//app.UseHttpLogging();

//swagger initialization
app.UseSwagger();
app.UseSwaggerUI(c =>
{
    c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{builder.Environment.ApplicationName} v1");
     
});

//Add authentication
app.UseAuthentication();
app.UseAuthorization();


//GetAll
app.MapGet("/api/todos", [Authorize] (TodosService todosService, ClaimsPrincipal user) =>
{
     var u = user;
     return Results.Ok(todosService.GetAll());
});//.RequireAuthorization();

//GetById
app.MapGet("/api/todos/{id}", [Authorize](int id, TodosService todosService, ILogger<Todo> logger) =>
{
    logger.LogInformation($"Receiced get request for Id: {id}");
    var todo = todosService.GetById(id);
    return todo != null ? Results.Ok(todo) : Results.NotFound();
});

//Create
app.MapPost("/api/todos",
    [Authorize](Todo todo, TodosService todosService, ILogger<Todo> logger, IValidator<Todo> validator) =>
    {
        logger.LogInformation($"Receiced POST request: {JsonSerializer.Serialize(todo)}");

        var validationResult = validator.Validate(todo);

        if (!validationResult.IsValid)
        {
            logger.LogError($"Invalid request received:{JsonSerializer.Serialize(todo)} ");
            logger.LogError($"Errors: {JsonSerializer.Serialize(validationResult.Errors)} ");
            return Results.BadRequest(
                validationResult.Errors
                    .Select(x => new { Property = x.PropertyName, Error = x.ErrorMessage }));
        }

        var id = todosService.Create(todo);
        logger.LogInformation($"Created new todo with id: {id}");
        return Results.Created($"/api/todos/{id}", null);
    });

//Update
app.MapPut("/api/todos/{id}",
    [Authorize](int id, Todo todo, TodosService todosService, ILogger<Todo> logger, IValidator<Todo> validator) =>
    {
        logger.LogInformation($"Receiced UPDATE request: {JsonSerializer.Serialize(todo)}");

        var validationResult = validator.Validate(todo);
        if (!validationResult.IsValid)
        {
            logger.LogError($"Invalid request received:{JsonSerializer.Serialize(todo)} \n\n" +
                            $" {JsonSerializer.Serialize(validationResult.Errors)} ");
            return Results.BadRequest(
                validationResult.Errors
                    .Select(x => new { Property = x.PropertyName, Error = x.ErrorMessage }));
        }

        todosService.Update(id, todo);
        logger.LogInformation($"Update  todo with id: {id}");
        return Results.Accepted();
    });

//Delete
app.MapDelete("/api/todos/{id}",
    [Authorize](int id, TodosService todosService, ILogger<Todo> logger) =>
    {
        return todosService.Delete(id)
            ? (Task)Results.NoContent()
            : (Task)Results.StatusCode((int)HttpStatusCode.InternalServerError);
    });

app.MapGet("/api/ping", () => "pong!");

app.Run();


//Todos Service
public record Todo(int? Id, string Title, bool IsCompleted);

public class TodoValidator : AbstractValidator<Todo>
{
    public TodoValidator() => RuleFor(x => x.Title).NotNull().WithMessage("Title required");
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


  public class BasicAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
       private readonly UserService _userService;

        public BasicAuthenticationHandler(
            IOptionsMonitor<AuthenticationSchemeOptions> options,
            ILoggerFactory logger,
            UrlEncoder encoder,
            ISystemClock clock, 
            UserService userService
            )
            : base(options, logger, encoder, clock)
        {
          _userService = userService;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            // skip authentication if endpoint has [AllowAnonymous] attribute
            var endpoint = Context.GetEndpoint();
            if (endpoint?.Metadata?.GetMetadata<IAllowAnonymous>() != null)
                return AuthenticateResult.NoResult();

            
            if (!Request.Headers.ContainsKey("Authorization"))
                return AuthenticateResult.Fail("Missing Authorization Header");

            User user= null;
            
            try
            {
                var authHeader = AuthenticationHeaderValue.Parse(Request.Headers["Authorization"]);
                var credentialBytes = Convert.FromBase64String(authHeader.Parameter);
                var credentials = Encoding.UTF8.GetString(credentialBytes).Split(new[] { ':' }, 2);
                var username = credentials[0];
                var password = credentials[1];
                user = await _userService.Authenticate(username, password);
            }
            catch
            {
                return AuthenticateResult.Fail("Invalid Authorization Header");
            }

            if (user == null)
                return AuthenticateResult.Fail("Invalid Username or Password");

            var claims = new[] {
              //  new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
                new Claim(ClaimTypes.Name, user.Username),
            };
            var identity = new ClaimsIdentity(claims, Scheme.Name);
            var principal = new ClaimsPrincipal(identity);
            var ticket = new AuthenticationTicket(principal, Scheme.Name);

            return AuthenticateResult.Success(ticket);
        }
    }
    
    public record User(String Username);

public class UserService
{
    public async Task<User?> Authenticate(string username, string password) =>
        //TODO: add db logic
        await Task.FromResult( !(string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))?  new User(username) : null);
}
    
    