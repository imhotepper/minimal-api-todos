using System.Net;
using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.OpenApi.Models;
using System.Text.Json;
using System.Text.Json.Serialization;
using AutoMapper;
using FluentValidation;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Options;


var builder = WebApplication.CreateBuilder(args);


//FluentValidation
builder.Services.AddValidatorsFromAssemblyContaining<Todo>(lifetime: ServiceLifetime.Scoped);

//swagger registration
builder.Services.AddEndpointsApiExplorer();
#region Basic
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { Title = builder.Environment.ApplicationName, Version = "v1" });
    c.AddSecurityDefinition("basic", new OpenApiSecurityScheme
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
#endregion
#region Bearer
    c .AddSecurityDefinition("Bearer", new OpenApiSecurityScheme {  
        Description="JWT authorization using bearer scheme",
        Name = "Authorization",  
        Type = SecuritySchemeType.ApiKey,  
        In = ParameterLocation.Header,  
    });  
    c.AddSecurityRequirement(new OpenApiSecurityRequirement  {{  
            new OpenApiSecurityScheme {  
                Reference = new OpenApiReference {  
                    Type = ReferenceType.SecurityScheme,  
                    Id = "Bearer"  
                }  
            },Array.Empty<string>()}});
#endregion
});
//add services used by the api


//used for claims principal
builder.Services.AddHttpContextAccessor();
builder.Services.AddScoped<UserService>();
builder.Services.AddScoped<TodosService>();


// builder.Services.AddExceptionHandler();

builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
// builder.Logging.AddJsonConsole();
builder.Logging.AddConsole();
builder.Logging.AddSimpleConsole();

builder.Services.AddAuthentication("BasicAuthentication")
    .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>("BasicAuthentication", null);


// builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
//         .AddJwtBearer();

// builder.Services.AddAuthorization(options =>{
//     options.FallbackPolicy =  new AuthorizationPolicyBuilder()
//     .AddAuthenticationSchemes(JwtBearerDefaults.AuthenticationScheme)
//     .RequireAuthenticatedUser()
//     .Build();
// });

builder.Services.AddAuthorization();



var app = builder.Build();

app.UseHttpLogging();


if (!app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    //global error handler
    app.UseExceptionHandler((errorApp) =>
    {
        errorApp.Run(async (context) =>
        {
            var exceptionHandlerFeature =
                context.Features.Get<IExceptionHandlerFeature>();

            if (exceptionHandlerFeature?.Error != null)
                app.Logger.LogError(exceptionHandlerFeature?.Error,
                    "Global error logged again with some custom data!");

            context.Response.StatusCode = 500;
            context.Response.ContentType = "application/json";

            var errorMessage = new { Error = "Internal Server error! " };
            await context.Response.WriteAsync(JsonSerializer.Serialize(errorMessage));
        });
    });
}

// enable static file
app.UseFileServer();

// http logging
// app.UseHttpLogging();

//swagger initialization
app.UseSwagger();
app.UseSwaggerUI(c => { c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{builder.Environment.ApplicationName} v1"); });

//Add authentication
app.UseAuthentication();
app.UseAuthorization();


//GetAll
app.MapGet("/api/todos",
    [Authorize](IMapper automapper, TodosService todosService, ClaimsPrincipal user, ILogger<Todo> logger) =>
    {
        var u = user;
        logger.Log(LogLevel.Information, $"Current user: {JsonSerializer.Serialize(user.Identity)}");
        var mappedTodos = automapper.Map<List<TodoDto>>(todosService.GetAll());
        return Results.Ok(mappedTodos);
    }); //.RequireAuthorization();

//GetById
app.MapGet("/api/todos/{id}",
    [Authorize](int id, IMapper automapper, TodosService todosService, ILogger<Todo> logger) =>
    {
        logger.LogInformation($"Receiced get request for Id: {id}");
        var todo = todosService.GetById(id);
        return todo != null ? Results.Ok(automapper.Map<TodoDto>(todo)) : Results.NotFound();
    });

//Create
app.MapPost("/api/todos",
    [Authorize](TodoDto todoDto, IMapper automapper, TodosService todosService, ILogger<Todo> logger,
        IValidator<Todo> validator) =>
    {
        logger.LogInformation($"Receiced POST request: {JsonSerializer.Serialize(todoDto)}");

        var todo = automapper.Map<Todo>(todoDto);

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
    [Authorize](int id, TodoDto todoDto, IMapper automapper, TodosService todosService, ILogger<Todo> logger,
        IValidator<Todo> validator) =>
    {
        logger.LogInformation($"Receiced UPDATE request: {JsonSerializer.Serialize(todoDto)}");

        var todo = automapper.Map<Todo>(todoDto);

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
        logger.LogInformation($"Updated todo with id: {id}");
        return Results.Accepted();
    });

//Delete
app.MapDelete("/api/todos/{id}",
    [Authorize](int id, TodosService todosService, ILogger<Todo> logger) =>
        todosService.Delete(id)
            ? Results.NoContent()
            : Results.StatusCode((int)HttpStatusCode.InternalServerError));

app.MapGet("/api/error", () =>
{
    throw new ApplicationException("Ups ... something went wrong.");
}).AllowAnonymous();

app.Run();


//Todos Service
public record TodoDto(int? Id, string Title, bool IsCompleted);

public class Todo
{
    public int? Id { get; set; }
    public string? Title { get; set; }
    public bool IsCompleted { get; set; }
    public string? UserName { get; set; }
}

public class TodoValidator : AbstractValidator<Todo>
{
    public TodoValidator() => RuleFor(x => x.Title).NotNull().WithMessage("Title required");
}

public class TodosService
{
    private readonly ClaimsPrincipal _user;

    public TodosService(IHttpContextAccessor httpContext) => _user = httpContext?.HttpContext?.User;

    //Target Typed new
    private static List<Todo> _todos = new List<Todo>();

    public int Create(Todo todo)
    {
        todo.UserName = _user.Identity?.Name;
        todo.Id = _todos.Count + 1;
        _todos.Add(todo);
        return todo.Id ?? -1;
    }

    public IEnumerable<Todo> GetAll() => _todos.Where(x => x.UserName == _user.Identity?.Name).ToList<Todo>();

    public bool Delete(int id) =>
        _todos.Remove(_todos.FirstOrDefault(x => x.Id == id && x.UserName == _user.Identity?.Name));

    public void Update(int id, Todo todo)
    {
        var td = _todos.FirstOrDefault(x => x.Id == id && x.UserName == _user.Identity?.Name);
        if (td == null) return;
        td.Title = todo.Title;
        td.IsCompleted = todo.IsCompleted;
    }

    public Todo? GetById(int id) => _todos.FirstOrDefault(x => x.Id == id && x.UserName == _user.Identity?.Name);
}

public record User(string Username, int Id = 1);


public class UserService
{
    public async Task<User?> Authenticate(string username, string password) =>
        await Task.FromResult(!(string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
            ? new User(username)
            : null);
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

        User? user = null;

        try
        {
            var authHeader = AuthenticationHeaderValue.Parse(Request.Headers["Authorization"]);
            if (authHeader.Parameter != null)
            {
                var credentialBytes = Convert.FromBase64String(authHeader.Parameter);
                var credentials = Encoding.UTF8.GetString(credentialBytes).Split(new[] { ':' }, 2);
                var username = credentials[0];
                var password = credentials[1];
                user = await _userService.Authenticate(username, password);
            }
        }
        catch
        {
            return AuthenticateResult.Fail("Invalid Authorization Header");
        }

        if (user == null)
            return AuthenticateResult.Fail("Invalid Username or Password");

        var claims = new[]
        {
            new Claim(ClaimTypes.NameIdentifier, user.Id.ToString()),
            new Claim(ClaimTypes.Name, user.Username),
        };
        var identity = new ClaimsIdentity(claims, Scheme.Name);
        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);

        return AuthenticateResult.Success(ticket);
    }
}

//Automapper
public class TodoProfile : Profile
{
    public TodoProfile()
    {
        CreateMap<Todo, TodoDto>().ReverseMap();
    }
}