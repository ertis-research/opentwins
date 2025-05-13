# opentwins-v2-prototype
Experimental prototype for OpenTwins version 2: focused on enhancing scalability and composability.

## Requirements
- .NET 8.0 SDK (v8.0.408) - Windows x64

dotnet new worker -n opentwins-twins -f net8.0
dotnet new worker -n opentwins-things -f net8.0
dotnet new webapi -n opentwins-orchestrator -f net8.0