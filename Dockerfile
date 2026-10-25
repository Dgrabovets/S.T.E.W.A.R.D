FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Steward.Schedule.Shared/Steward.Schedule.Shared.csproj Steward.Schedule.Shared/
COPY Steward.Schedule.Core/Steward.Schedule.Core.csproj Steward.Schedule.Core/
COPY Steward.Schedule/Steward.Schedule.csproj Steward.Schedule/
COPY tests/Steward.Schedule.Tests/Steward.Schedule.Tests.csproj tests/Steward.Schedule.Tests/
RUN dotnet restore Steward.Schedule/Steward.Schedule.csproj && dotnet restore tests/Steward.Schedule.Tests/Steward.Schedule.Tests.csproj
COPY Steward.Schedule.Shared Steward.Schedule.Shared
COPY Steward.Schedule.Core Steward.Schedule.Core
COPY Steward.Schedule Steward.Schedule
COPY tests/Steward.Schedule.Tests tests/Steward.Schedule.Tests
RUN dotnet test tests/Steward.Schedule.Tests/Steward.Schedule.Tests.csproj -c Release --no-restore
RUN dotnet publish Steward.Schedule/Steward.Schedule.csproj -c Release -o /app --no-restore

FROM mcr.microsoft.com/dotnet/runtime:10.0
WORKDIR /app
COPY --from=build /app .
ENTRYPOINT ["dotnet", "Steward.Schedule.dll"]
