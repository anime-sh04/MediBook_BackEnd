FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/MediBook.Schedule.API/MediBook.Schedule.API.csproj", "src/MediBook.Schedule.API/"]
RUN dotnet restore "src/MediBook.Schedule.API/MediBook.Schedule.API.csproj"
COPY . .
WORKDIR "/src/src/MediBook.Schedule.API"
RUN dotnet build "MediBook.Schedule.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MediBook.Schedule.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MediBook.Schedule.API.dll"]
