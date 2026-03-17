# FlowCare API
FlowCare is a RESTful API built with ASP.NET Core and PostgreSQL to manage branches, services, staff, slots, appointments, and audit logs.
---
## Technologies Used
- ASP.NET Core Web API
- Entity Framework Core
- PostgreSQL
- Git
- GitHub
---
## Setup Instructions
### 1. Install requirements
You need:
- .NET 8 SDK
- PostgreSQL
---
### 2. Create database
Example:
```sql
CREATE DATABASE flowcare_db;

⸻
3. Configure connection string
Update the connection string in appsettings.json:
"ConnectionStrings": {
  "DefaultConnection": "Host=localhost;Port=5432;Database=flowcare_db;Username=postgres;Password=postgres"
}

⸻
4. Add migrations
Run this command in package manager console:
## add-migration "createDbTables"
This will create the migration 
⸻
5. Run the project
Run:
run the project F5
Swagger will open at:
(https://localhost:7151/swagger/index.html)

⸻
Environment Variables
Example environment variables:
DefaultAdmin Username=admin
DefaultAdmin Password=Admin$123

⸻
Database Seeding
The project automatically seeds initial data on startup.
Seeded data includes:
• 2 branches
• 3 service types per branch
• managers and staff
• sample customers
• sample slots
Seeding is idempotent, meaning it will not duplicate data if run multiple times.
⸻
Example API Usage
Search Customers
GET /api/Customers?term={searchTerm}&page={pageNumber}&size={pageSize}
Example curl:
curl https://localhost:7151/api/Customers?term=99999999&page=1&size=10

⸻
Create appointment
POST /api/appointments
Body example:
{
 "slotId": 1,
 "serviceTypeId": 1
}

⸻
Get audit logs
GET /api/audit-logs

⸻
Slot Retention & Cleanup
Slots use soft delete.
Admin can configure retention days:
PUT /api/admin/settings/slot-retention-days
Cleanup endpoint removes expired slots:
POST /api/admin/slots/cleanup
Cleanup is idempotent and safe.
⸻
Migration Scripts
Database schema is managed using Entity Framework Core migrations.
Run in package manager console:
##update-database
to apply migrations.
⸻
Author
Sabrina Al Barwani
---
