FROM mcr.microsoft.com/dotnet/aspnet:9.0
WORKDIR /app
COPY ./bin/publish ./
ENTRYPOINT ["dotnet", "./CollectionBooking.Host.dll"]
