#See https://aka.ms/containerfastmode to understand how Visual Studio uses this Dockerfile to build your images for faster debugging.

FROM mcr.microsoft.com/dotnet/aspnet:5.0-buster-slim AS base
WORKDIR /app
EXPOSE 80
EXPOSE 443

FROM mcr.microsoft.com/dotnet/sdk:5.0-buster-slim AS build
WORKDIR /src
COPY ["src/Nodsoft.YumeChan.NetRunner/Nodsoft.YumeChan.NetRunner.csproj", "src/Nodsoft.YumeChan.NetRunner/"]
COPY ["src/Nodsoft.YumeChan.Core/Nodsoft.YumeChan.Core.csproj", "src/Nodsoft.YumeChan.Core/"]
COPY ["src/Nodsoft.YumeChan.PluginBase/Nodsoft.YumeChan.PluginBase.csproj", "src/Nodsoft.YumeChan.PluginBase/"]
COPY ["src/Nodsoft.YumeChan.Essentials/Nodsoft.YumeChan.Essentials.csproj", "src/Nodsoft.YumeChan.Essentials/"]
RUN dotnet restore "src/Nodsoft.YumeChan.NetRunner/Nodsoft.YumeChan.NetRunner.csproj"
COPY . .
WORKDIR "/src/src/Nodsoft.YumeChan.NetRunner"
RUN dotnet build "Nodsoft.YumeChan.NetRunner.csproj" -c Release -o /app/build

FROM build AS publish
RUN dotnet publish "Nodsoft.YumeChan.NetRunner.csproj" -c Release -o /app/publish

FROM base AS final
WORKDIR /app
COPY --from=publish /app/publish .
ENTRYPOINT ["dotnet", "Nodsoft.YumeChan.NetRunner.dll"]

