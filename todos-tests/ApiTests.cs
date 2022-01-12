using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Hosting;
using Xunit;

namespace minimal_todos_tests;

public class ApiTests
{
    private readonly HttpClient _client;

    public ApiTests()
    {
        var application = new TodosApplication();
        _client = application.CreateClient();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic",
            Convert.ToBase64String(Encoding.UTF8.GetBytes("username:password")));
    }

    [Fact]
    public async Task Can_call_get_todos()
    {
        var todos = await _client.GetFromJsonAsync<List<TodoDto>>("/api/todos");
        Assert.True(todos is List<TodoDto>);
    }

    [Fact]
    public async Task Can_post_todo()
    {
        var response = await _client.PostAsJsonAsync("/api/todos",
            new TodoDto(null, Title: "I want to do this thing tomorrow", IsCompleted: false));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var todos = await _client.GetFromJsonAsync<List<Todo>>("/api/todos");

        var todo = todos.Last();
        Assert.Equal("I want to do this thing tomorrow", todo.Title);
        Assert.False(todo.IsCompleted);
    }

    [Fact]
    public async Task Can_delete_todo()
    {
        var response = await _client.PostAsJsonAsync("/api/todos",
            new TodoDto(null, Title: "I want to do this thing tomorrow", IsCompleted: false));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);

        var todos = await _client.GetFromJsonAsync<List<TodoDto>>("/api/todos");

        Assert.True(todos.Count > 0);
        var todo = todos.ToList().Last();
        Assert.Equal("I want to do this thing tomorrow", todo.Title);
        Assert.False(todo.IsCompleted);

        response = await _client.DeleteAsync($"/api/todos/{todo.Id}");

        Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);

        response = await _client.GetAsync($"/api/todos/{todo.Id}");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task Can_update_todo()
    {
        var response = await _client.PostAsJsonAsync("/api/todos",
            new TodoDto(null, Title: "I want to do this thing tomorrow", IsCompleted: false));

        Assert.Equal(HttpStatusCode.Created, response.StatusCode);


        var todos = await _client.GetFromJsonAsync<List<TodoDto>>("/api/todos");

        var todo = Assert.Single(todos);
        Assert.Equal("I want to do this thing tomorrow", todo.Title);
        Assert.False(todo.IsCompleted);

        var nextTodo = todo with {  Title = todo.Title + todo.IsCompleted, IsCompleted = todo.IsCompleted };

        response = await _client.PutAsJsonAsync($"/api/todos/{todo.Id}", nextTodo);

        Assert.Equal(HttpStatusCode.Accepted, response.StatusCode);

        var updatedTodo = await _client.GetFromJsonAsync<TodoDto>($"/api/todos/{todo.Id}");

        Assert.Equal(updatedTodo.Title, nextTodo.Title);
        Assert.Equal(updatedTodo.IsCompleted, nextTodo.IsCompleted);
    }

    private class TodosApplication : WebApplicationFactory<Program>
    {
        protected override IHost CreateHost(IHostBuilder builder)
        {
            
            builder.ConfigureServices(services =>
            {
                // services.RemoveAll(typeof(DbContextOptions<TodoDbContext>));
                //
                // services.AddDbContext<TodoDbContext>(options =>
                //     options.UseInMemoryDatabase("Testing", root));
            });

            return base.CreateHost(builder);
        }
    }
}