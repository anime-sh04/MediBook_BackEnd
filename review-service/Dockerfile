FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ["src/MediBook.Review.API/MediBook.Review.API.csproj", "src/MediBook.Review.API/"]
RUN dotnet restore "src/MediBook.Review.API/MediBook.Review.API.csproj"
COPY . .
WORKDIR "/src/src/MediBook.Review.API"
RUN dotnet build "MediBook.Review.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MediBook.Review.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MediBook.Review.API.dll"]
