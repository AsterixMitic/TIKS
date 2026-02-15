FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /app

COPY *.slnx ./
COPY NppCore/*.csproj ./NppCore/
COPY NppApi/*.csproj ./NppApi/
COPY NppTests/NppApi.ComponentTests/*.csproj ./NppTests/NppApi.ComponentTests/
COPY NppTests/Npp.PlaywrightTests/*.csproj ./NppTests/Npp.PlaywrightTests/

RUN dotnet restore

COPY . .

WORKDIR /app/NppApi

EXPOSE 8080

CMD ["dotnet", "watch", "run", "--urls", "http://0.0.0.0:8080"]
