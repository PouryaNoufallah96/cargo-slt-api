FROM mcr.microsoft.com/dotnet/aspnet:8.0
WORKDIR /app
EXPOSE 80
EXPOSE 443
ADD ./publish/ .
ENTRYPOINT ["dotnet", "SLT.Api.dll"]