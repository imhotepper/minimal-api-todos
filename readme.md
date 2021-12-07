
# Todos APi minimal API

## Features

- [x] serve html for landing page
-  crud api
- mapper(mapster or automaper)
- minimal validation
-  todosService
-  swagger
- deploy 



## Serve html for landing page

In order to serve html files from the ```wwwroot``` folder add the followings:
- create a folder ``` wwwroot ``` in the root of the project
- inside ```program.cs``` add the following line so that html files can be served to the root url of the api
```code
app.UseFileServer();
```

Once the up items are present the ```index.html``` from the ```wwwroot``` folder will be served
