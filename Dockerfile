FROM node:24.22.3-bookworm-slim AS frontend
WORKDIR /source/frontend
COPY frontend/package.json frontend/package-lock.json ./
RUN npm ci
COPY frontend/ ./
RUN npm run build

FROM mcr.microsoft.com/dotnet/sdk:10.0.100 AS build
WORKDIR /source
COPY global.json Directory.Build.props Directory.Packages.props ./
COPY dataset/schemas/ dataset/schemas/
COPY src/InvoiceDesk.Core/ src/InvoiceDesk.Core/
COPY src/InvoiceDesk.Web/ src/InvoiceDesk.Web/
RUN dotnet restore src/InvoiceDesk.Web --locked-mode
COPY --from=frontend /source/frontend/dist/ src/InvoiceDesk.Web/wwwroot/
RUN dotnet publish src/InvoiceDesk.Web -c Release --no-restore -o /publish

FROM mcr.microsoft.com/dotnet/aspnet:10.0.15
WORKDIR /app
RUN apt-get update \
    && apt-get install -y --no-install-recommends curl \
    && rm -rf /var/lib/apt/lists/* \
    && install -d -o "$APP_UID" -g "$APP_UID" /data
COPY --from=build /publish/ ./
USER $APP_UID
ENV ASPNETCORE_HTTP_PORTS=8080
EXPOSE 8080
ENTRYPOINT ["dotnet", "InvoiceDesk.Web.dll"]
