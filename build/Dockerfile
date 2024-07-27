# syntax=docker/dockerfile:1.4

# Stage 1: Build the application
FROM mcr.microsoft.com/dotnet/sdk:8.0 AS build
WORKDIR /src
COPY ./src .
RUN dotnet publish "./SmtpServerService.sln" -c Release --restore

# Stage 2: Create the final image
FROM mcr.microsoft.com/dotnet/runtime:8.0 AS runtime
WORKDIR /app
COPY --from=build /src/SmtpServerService/bin/Release/net8.0/publish/ .

# Create the attachments directory and set the environment variable
ENV SMTP_OUTPUTPATH=/attachments
ENV SMTP_SMTPPORT=25000
RUN mkdir -p /attachments

# Expose port 25
EXPOSE 25000

# Command to run the application
ENTRYPOINT ["./SmtpServerService"]
# ENTRYPOINT [ "/bin/sh"]
