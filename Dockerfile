
FROM mcr.microsoft.com/dotnet/sdk:10.0

WORKDIR /app

# Restore dependencies
COPY backend.csproj ./
RUN dotnet restore backend.csproj

# Copy the remaining application files
COPY . .

EXPOSE 5000

CMD ["dotnet", "watch", "run", "--project", "backend.csproj", "--urls", "http://0.0.0.0:5000"]
