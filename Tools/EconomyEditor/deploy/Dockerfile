# CI supplies a fresh, tested publish directory and catalog as the build context.
FROM mcr.microsoft.com/dotnet/aspnet:10.0
WORKDIR /app
COPY app/ ./
COPY catalog/ ./catalog/
ENV ASPNETCORE_URLS=http://+:8080
ENV catalog=/app/catalog
ENV data=/data
EXPOSE 8080
HEALTHCHECK --interval=15s --timeout=5s --start-period=20s --retries=3 CMD ["dotnet", "EconomyEditor.dll", "--healthcheck"]
ENTRYPOINT ["dotnet", "EconomyEditor.dll"]
