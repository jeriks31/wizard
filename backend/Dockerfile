FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src

COPY Wizard.slnx ./
COPY src/Wizard.Game/Wizard.Game.csproj src/Wizard.Game/
COPY src/Wizard.Api/Wizard.Api.csproj src/Wizard.Api/
RUN dotnet restore src/Wizard.Api/Wizard.Api.csproj

COPY src ./src
RUN dotnet publish src/Wizard.Api/Wizard.Api.csproj -c Release -o /app/out /p:UseAppHost=false

FROM mcr.microsoft.com/dotnet/aspnet:10.0 AS final
WORKDIR /app
COPY --from=build /app/out ./
ENV ASPNETCORE_URLS=http://+:8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "Wizard.Api.dll"]
