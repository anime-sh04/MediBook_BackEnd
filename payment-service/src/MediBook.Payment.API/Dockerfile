FROM mcr.microsoft.com/dotnet/aspnet:8.0 AS base
WORKDIR /app
EXPOSE 8080

FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src

COPY ["src/MediBook.Payment.API/MediBook.Payment.API.csproj", "src/MediBook.Payment.API/"]
RUN dotnet restore "src/MediBook.Payment.API/MediBook.Payment.API.csproj"

COPY . .

WORKDIR "/src/MediBook.Payment.API"   # ✅ FIXED (removed extra src)

RUN dotnet build "MediBook.Payment.API.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "MediBook.Payment.API.csproj" -c Release -o /app/publish /p:UseAppHost=false

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "MediBook.Payment.API.dll"]