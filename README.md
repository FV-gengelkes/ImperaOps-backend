# FreightVis Backend (Local MVP)

## Prereqs
- .NET SDK 9.x
- Docker + Docker Compose

## Start MySQL
```bash
docker compose up -d
```
Adminer: http://localhost:8080  
MySQL: localhost:3306 (db: freightvis, user: freightvis, pass: freightvis)

## Run API
```bash
dotnet run --project src/FreightVis.Api
```
API: http://localhost:5000  
Swagger: http://localhost:5000/swagger  
Health: http://localhost:5000/health

## MVP endpoints
- POST `/api/v1/incidents`
- GET `/api/v1/incidents?clientId={guid}&page=1&pageSize=25`
- GET `/api/v1/incidents/{id}`
- PUT `/api/v1/incidents/{id}`
