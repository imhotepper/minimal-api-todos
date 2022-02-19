using System.ComponentModel.DataAnnotations;
using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Security.Claims;
using System.Text;
using Microsoft.OpenApi.Models;
using System.Text.Json;
using AutoMapper;
using FluentValidation;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Diagnostics;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.IdentityModel.Tokens;
using MiniValidation;
using BC = BCrypt.Net.BCrypt;


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
// builder.Services.AddScoped<DataProtector>();
builder.Services.AddMemoryCache();


builder.Services.AddAutoMapper(AppDomain.CurrentDomain.GetAssemblies());
builder.Logging.AddConsole();
builder.Logging.AddSimpleConsole();

builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
        .AddJwtBearer(o =>
        {
            o.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidateAudience = true,
                ValidateLifetime = false,
                ValidateIssuerSigningKey = true,
                ValidIssuer = builder.Configuration["Jwt:Issuer"],
                ValidAudience = builder.Configuration["Jwt:Audience"],
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"]))
            };
        });


builder.Services.AddAuthorization();

var app = builder.Build();

// http logging
app.UseHttpLogging();

// Erorr handling
if (app.Environment.IsDevelopment())
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
                app.Logger.LogError(exceptionHandlerFeature.Error,
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

        #region mini-validation

        if (!MiniValidator.TryValidate(todo, out var errors))
        {
            logger.LogError($"Invalid request received:{JsonSerializer.Serialize(todo)} ");
            logger.LogError($"Errors: {JsonSerializer.Serialize(errors)} ");

            return Results.BadRequest(
                errors.Keys.SelectMany(x => errors[x].Select(y => new { Property = x, Error = y }))
            );
        }
       
        #endregion
   
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

        #region Fluent Validation

        var validationResult = validator.Validate(todo);
        if (!validationResult.IsValid)
        {
            logger.LogError($"Invalid request received:{JsonSerializer.Serialize(todo)} \n\n" +
                            $" {JsonSerializer.Serialize(validationResult.Errors)} ");
            return Results.BadRequest(
                validationResult.Errors
                    .Select(x => new { Property = x.PropertyName, Error = x.ErrorMessage }));
        }

        #endregion

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

app.MapGet("/api/error", () 
    =>
{
    throw new ApplicationException("Ups ... something went wrong.");
}).AllowAnonymous();

app.MapPost("/api/register", (NewUserRequest newUser, UserService userService) =>
{
    //If present return 400 bad req
    var user = userService.Register(newUser);
    if (user == null) return Results.BadRequest("Username taken ");
    
    //get token
    return Results.Ok(userService.GetToken(user));
});
app.MapPost("/api/token", (NewUserRequest userRequest, UserService userService) =>
{
     var user = userService.ValidateUser(userRequest);
    if (user == null) return Results.Unauthorized();
    var token = userService.GetToken(user);
        return Results.Ok(token);
   
   
});

app.Run();


//Todos Service
public record TodoDto(int? Id, string Title, bool IsCompleted);

public class Todo
{
    public int? Id { get; set; }
    [Required]
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
    private readonly ClaimsPrincipal? _user;

    public TodosService(IHttpContextAccessor httpContext) => _user = httpContext.HttpContext?.User;

    //Target Typed new
    private static List<Todo> _todos = new List<Todo>();

    public int Create(Todo todo)
    {
        todo.UserName = _user?.Identity?.Name;
        todo.Id = _todos.Count + 1;
        _todos.Add(todo);
        return todo.Id ?? -1;
    }

    public IEnumerable<Todo> GetAll() => _todos.Where(x => x.UserName == _user?.Identity?.Name).ToList();

    public bool Delete(int id) =>
        _todos.Remove(_todos.FirstOrDefault(x => x.Id == id && x.UserName == _user?.Identity?.Name));

    public void Update(int id, Todo todo)
    {
        var td = _todos.FirstOrDefault(x => x.Id == id && x.UserName == _user?.Identity?.Name);
        if (td == null) return;
        td.Title = todo.Title;
        td.IsCompleted = todo.IsCompleted;
    }

    public Todo? GetById(int id) => _todos.FirstOrDefault(x => x.Id == id && x.UserName == _user.Identity?.Name);
}

public record User(string Username, string? PasswordHash , int Id = 1);

public record NewUserRequest(string userName, string password);

public class UserService
{
    private readonly IConfiguration _configuration;

    private readonly string UsersKey = "users-list";
    private List<User> _users;
    public UserService(IMemoryCache memoryCache, IConfiguration configuration)
    {
        _configuration = configuration;
        if(memoryCache.TryGetValue(UsersKey, out List<User> usersList))
            _users = usersList;
        else
        {
            _users = new();
            memoryCache.Set(UsersKey, _users);
        }
    }
    
    public User ValidateUser(NewUserRequest newUser)
    {
        var user = _users.FirstOrDefault(x => x.Username == newUser.userName);
        if (user == null) return null;
        
        return BC.Verify(newUser.password, user.PasswordHash) ? user : null;
    }

    public User? Register(NewUserRequest newUser)
    {
        //return null if user exists
        if (_users.Any(x => x.Username == newUser.userName)) return null;
       
        var user = new User(newUser.userName,  BCrypt.Net.BCrypt.HashPassword(newUser.password), _users.Count+1);
        _users.Add(user);
        return user;
    }
    
    public string GetToken(User user)
    {
        var validIssuer = _configuration["Jwt:Issuer"];
        var validAudience = _configuration["Jwt:Audience"];
        var signingKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_configuration["Jwt:Key"]));
        var credentials = new SigningCredentials(signingKey, SecurityAlgorithms.HmacSha256);
        var tokenClaims = new JwtSecurityToken(claims: new []
            {
                new Claim("name",user.Username), 
                new Claim(  "sub",  user.Id.ToString())
            }, issuer: validIssuer, audience: validAudience,
            signingCredentials: credentials);
        var token = new JwtSecurityTokenHandler().WriteToken(tokenClaims);
        return token;
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


//Data protection service
// public class DataProtector
// {
//     private readonly IDataProtector _protector;
//
//     public DataProtector( IDataProtectionProvider dataProtector, IConfiguration config) => 
//         _protector = dataProtector.CreateProtector(config["Jwt:Key"]);
//
//     public string Protect(string value) => _protector.Protect(value);
//     public string Unprotect(string value) => _protector.Unprotect(value);
// }


public static class ClaimsPrincipalExtension
{
    public static T GetUserId<T>(this ClaimsPrincipal user)
    {
        var uid = user.Claims.FirstOrDefault(x => x.Type == "sub");
        if (uid != null)
            return (T)Convert.ChangeType(uid, typeof(T));
        else
            throw new ApplicationException("unable to find user inside the received claims!");
    }
}