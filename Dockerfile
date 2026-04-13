FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

# Copy solution and project files first for layer caching
COPY Directory.Build.props Directory.Packages.props ./
COPY src/Wick.Core/Wick.Core.csproj src/Wick.Core/
COPY src/Wick.Providers.CSharp/Wick.Providers.CSharp.csproj src/Wick.Providers.CSharp/
COPY src/Wick.Providers.GDScript/Wick.Providers.GDScript.csproj src/Wick.Providers.GDScript/
COPY src/Wick.Providers.Godot/Wick.Providers.Godot.csproj src/Wick.Providers.Godot/
COPY src/Wick.Runtime/Wick.Runtime.csproj src/Wick.Runtime/
COPY src/Wick.Server/Wick.Server.csproj src/Wick.Server/
RUN dotnet restore src/Wick.Server/Wick.Server.csproj

# Copy everything and publish
COPY . .
RUN dotnet publish src/Wick.Server/Wick.Server.csproj -c Release -o /app

FROM mcr.microsoft.com/dotnet/runtime:10.0 AS runtime
WORKDIR /app
COPY --from=build /app .

ENTRYPOINT ["dotnet", "Wick.Server.dll"]
