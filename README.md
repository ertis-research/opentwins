# opentwins-v2-prototype
Experimental prototype for OpenTwins version 2: focused on enhancing scalability and composability.

## Requirements
- .NET 8.0 SDK (v8.0.408) - Windows x64

winget install Dapr.CLI 

En consola con admin:
dapr init


dapr --version
Output:
CLI version: 1.15.1 
Runtime version: 1.15.4

Tenemos 4 proyectos, cada uno por cada componente de la arquictura que tiene que ser desarrollado.


Crear los proyectos .NET:
dotnet new webapi -n Twins -f net8.0
dotnet new webapi -n Things -f net8.0
dotnet new webapi -n Orchestrator -f net8.0

Añadir los paquetes a cada proyecto que lo necesite:
dotnet add package Dapr.Client
dotnet add package Dapr.Actors.AspNetCore
dotnet add package Confluent.Kafka
dotnet add package Dapr.AspNetCore

Ejecutar el proyecto (los contenedores de dapr tienen que estar iniciados!):
dapr run --app-id myapp --app-port 5000 --dapr-http-port 3500 -- dotnet run