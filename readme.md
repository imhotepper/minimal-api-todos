
# Todos APi minimal API

## Features

- [x] serve html for landing page
- [ ]  swagger
-  crud api
- mapper(mapster or automaper)
- minimal validation
-  todosService

- deploy 



## Serve html for landing page

In order to serve html files from the ```wwwroot``` folder add the followings:
- create a folder ``` wwwroot ``` in the root of the project
- inside ```program.cs``` add the following line so that html files can be served to the root url of the api
```code
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

```
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = builder.Environment.ApplicationName, Version = "v1" });
});
```

In order to use it in the browser we have to allign  with the ```app``` so add the following lines after ```var app = builder.Build();```:

```
 app.UseSwagger();
app.UseSwaggerUI(c => c.SwaggerEndpoint("/swagger/v1/swagger.json", $"{builder.Environment.ApplicationName} v1"));
```

Ok, now if we restart the api and navigate to ```...../swagger``` we will be presented with the OpenApi definitions.



## CRUD Api

### Create

### Get All

### Get by Id

### Update

### Delete