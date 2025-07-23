# Use a imagem SDK do .NET 9.0 como base para build
FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /src

# Copiar os arquivos do projeto
COPY ["Serel.MCPServer.SqlServer.csproj", "./"]
RUN dotnet restore "Serel.MCPServer.SqlServer.csproj"
COPY . .

# Publicar a aplicação
RUN dotnet publish "Serel.MCPServer.SqlServer.csproj" -c Release -o /app/publish /p:UseAppHost=false

# Build da imagem final
FROM mcr.microsoft.com/dotnet/runtime:9.0 AS final
WORKDIR /app
COPY --from=build /app/publish .

# Listar arquivos para debug
RUN ls -la

# Configurar variável de ambiente (valor default vazio)
ENV SQL_CONNECTION_STRING=""

# Configurar o ponto de entrada
ENTRYPOINT ["dotnet", "Serel.MCPServer.SqlServer.dll"]
