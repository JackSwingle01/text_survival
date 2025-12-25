FROM mcr.microsoft.com/dotnet/sdk:9.0 AS build
WORKDIR /app

# Copy project file and restore (use csproj directly, not sln)
COPY text_survival.csproj ./
RUN dotnet restore text_survival.csproj

# Copy everything else and publish
COPY . .
RUN dotnet publish text_survival.csproj -c Release -o out

# Runtime stage
FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY --from=build /app/out .

EXPOSE 8080
ENV ASPNETCORE_URLS=http://+:8080

ENTRYPOINT ["dotnet", "text_survival.dll", "--web", "--port=8080"]
