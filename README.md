# Management API

Majlis Ansarullah Nigeria Management System - Backend API

## Architecture

This project follows **Clean Architecture** (Onion Architecture) with the following layers:

- **Domain**: Core business entities, enums, events, and interfaces
- **Application**: Business logic, CQRS commands/queries, DTOs, validators
- **Infrastructure**: External concerns (Database, Identity, Services)
- **Host**: ASP.NET Core Web API entry point

## Tech Stack

- **.NET 9.0**
- **Entity Framework Core 9.0** with SQL Server
- **ASP.NET Core Identity** for authentication
- **MediatR** for CQRS pattern
- **FluentValidation** for request validation
- **Mapster** for object mapping
- **Ardalis.Specification** for repository pattern
- **Serilog** for logging
- **Swagger/OpenAPI** for API documentation
- **Docker** for containerization

## Getting Started

### Prerequisites

- .NET 9 SDK
- SQL Server (or Docker)
- Visual Studio 2022 / VS Code / Rider

### Running Locally

1. **Update Connection String** in `src/Host/appsettings.Development.json`

2. **Apply Migrations**
   ```bash
   cd src/Host
   dotnet ef database update --project ../Infrastructure
   ```

3. **Run the API**
   ```bash
   dotnet run --project src/Host/Host.csproj
   ```

4. **Access Swagger UI**
   - Navigate to: `https://localhost:5001` or `http://localhost:5000`

### Running with Docker

1. **Build and Run**
   ```bash
   docker-compose up --build
   ```

2. **Access API**
   - API: `http://localhost:5100`
   - SQL Server: `localhost:1433` (SA password: `YourStrong@Passw0rd`)

3. **Stop Services**
   ```bash
   docker-compose down
   ```

## Project Structure

```
ManagementApi/
├── src/
│   ├── Core/
│   │   ├── Domain/           # Entities, Enums, Events, Interfaces
│   │   ├── Application/      # CQRS, DTOs, Validators, Behaviors
│   │   └── Shared/          # Authorization (Permissions, Roles)
│   ├── Infrastructure/       # DbContext, Repositories, Identity, Services
│   └── Host/                # API Controllers, Program.cs, Configurations
├── tests/
│   ├── Application.Tests/
│   └── Infrastructure.Tests/
├── Dockerfile
├── docker-compose.yml
└── ManagementApi.sln
```

## Features

### Implemented
- ✅ Onion Architecture with Clean Code principles
- ✅ CQRS pattern with MediatR
- ✅ Repository pattern with Specifications
- ✅ Domain-Driven Design (Aggregates, Events)
- ✅ Entity Framework Core with SQL Server
- ✅ ASP.NET Core Identity (Custom User/Role)
- ✅ JWT Authentication
- ✅ Permission-based Authorization
- ✅ FluentValidation pipeline
- ✅ Serilog logging
- ✅ Swagger/OpenAPI documentation
- ✅ Docker support

### In Progress
- 🚧 Authentication endpoints (Register, Login)
- 🚧 Membership Management
- 🚧 Jamaat-to-Muqam Mapping
- 🚧 Reports Management
- 🚧 Organization Management

## API Endpoints

### Authentication
- `POST /api/v1/auth/register` - Register new user
- `POST /api/v1/auth/login` - Login user
- `GET /api/v1/auth/me` - Get current user

### Members
- `GET /api/v1/members` - Get all members
- `GET /api/v1/members/{id}` - Get member by ID
- `POST /api/v1/members/search` - Search members (with filters)
- `PUT /api/v1/members/{id}` - Update member
- `POST /api/v1/members/{id}/photo` - Upload member photo

### Jamaats
- `GET /api/v1/jamaats` - Get all jamaats
- `POST /api/v1/jamaats/map` - Map jamaat to muqam
- `DELETE /api/v1/jamaats/{id}/unmap` - Unmap jamaat

### Reports
- `GET /api/v1/reports` - Get report submissions
- `POST /api/v1/reports` - Submit report
- `PUT /api/v1/reports/{id}` - Update report draft
- `POST /api/v1/reports/{id}/approve` - Approve report
- `POST /api/v1/reports/{id}/reject` - Reject report

## Authentication & Authorization

### Registration
Users register with **ChandaNo** (membership number) and **Password** (minimum 3 characters).
The system fetches user information from the Members table automatically.

### Default Roles
- **Admin** - Has all permissions
- **Member** - Default role for all users (cannot be removed)
- **National Secretary** - National level access
- **Zonal Coordinator** - Zone level access
- **Nazim A'ala** - Dila (district) level access
- **Zaim A'ala** - Muqam (local) level access

### Permissions
Permission-based authorization with 40+ granular permissions:
- Members.View, Members.Create, Members.Edit
- Reports.Submit, Reports.Approve, Reports.ViewAnalytics
- Users.AssignRoles, Roles.ManagePermissions
- etc.

### JWT Token
Tokens include:
- User ID, Email, ChandaNo
- Organization context (MuqamId, DilaId, ZoneId)
- All user roles
- All user permissions

## Database Migrations

### Create Migration
```bash
cd src/Host
dotnet ef migrations add MigrationName --project ../Infrastructure
```

### Apply Migration
```bash
dotnet ef database update --project ../Infrastructure
```

### Remove Last Migration
```bash
dotnet ef migrations remove --project ../Infrastructure
```

## Testing

### Run Tests
```bash
dotnet test
```

### Run with Coverage
```bash
dotnet test /p:CollectCoverage=true /p:CoverletOutput=./coverage/
```

## Configuration

Key configuration files in `src/Host/`:
- `appsettings.json` - Production settings
- `appsettings.Development.json` - Development settings

### Important Settings
- **ConnectionStrings:DefaultConnection** - Database connection
- **JwtSettings:SecretKey** - JWT signing key (change in production!)
- **JwtSettings:ExpirationHours** - Token expiration (default: 24 hours)

## Contributing

1. Create a feature branch
2. Implement changes following Clean Architecture principles
3. Write unit tests
4. Submit pull request

## License

Proprietary - Majlis Ansarullah Nigeria
