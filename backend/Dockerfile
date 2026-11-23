# --- build ---
FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Barlas.slnx ./
COPY src/Barlas.Domain/*.csproj src/Barlas.Domain/
COPY src/Barlas.Application/*.csproj src/Barlas.Application/
COPY src/Barlas.Infrastructure/*.csproj src/Barlas.Infrastructure/
COPY src/Barlas.Api/*.csproj src/Barlas.Api/
RUN dotnet restore src/Barlas.Api/Barlas.Api.csproj
COPY src/ src/
RUN dotnet publish src/Barlas.Api/Barlas.Api.csproj -c Release -o /app --no-restore

# --- runtime ---
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /app .
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
USER $APP_UID
ENTRYPOINT ["dotnet", "Barlas.Api.dll"]
