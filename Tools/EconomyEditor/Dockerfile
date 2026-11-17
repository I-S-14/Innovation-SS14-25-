FROM mcr.microsoft.com/dotnet/sdk:10.0 AS build
WORKDIR /src
COPY Directory.Packages.props ./
COPY RobustToolbox/Directory.Packages.props ./RobustToolbox/
COPY Tools/EconomyEditor/EconomyEditor.csproj ./Tools/EconomyEditor/
RUN dotnet restore Tools/EconomyEditor/EconomyEditor.csproj
COPY Tools/EconomyEditor/*.cs ./Tools/EconomyEditor/
COPY Tools/EconomyEditor/wwwroot/ ./Tools/EconomyEditor/wwwroot/
RUN dotnet publish Tools/EconomyEditor/EconomyEditor.csproj --no-restore -c Release -o /out

FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY --from=build /out/ .
COPY Tools/EconomyEditor/data/catalog/ /app/catalog/
ENV ASPNETCORE_URLS=http://+:8080
ENV catalog=/app/catalog
ENV data=/data
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=3 CMD ["dotnet", "EconomyEditor.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "EconomyEditor.dll"]
