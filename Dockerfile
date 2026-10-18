FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY UltimateParser.slnx ./
COPY UltimateParser.Shared/UltimateParser.Shared.csproj UltimateParser.Shared/
COPY UltimateParser.Core/UltimateParser.Core.csproj UltimateParser.Core/
COPY UltimateParser/UltimateParser.csproj UltimateParser/
COPY UltimateParser.Tests/UltimateParser.Tests.csproj UltimateParser.Tests/
COPY UltimateParser.Tray/UltimateParser.Tray.csproj UltimateParser.Tray/
RUN dotnet restore UltimateParser.slnx
COPY . .
RUN dotnet test UltimateParser.Tests/UltimateParser.Tests.csproj -c Release --no-restore
RUN dotnet publish UltimateParser/UltimateParser.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "UltimateParser.dll"]
