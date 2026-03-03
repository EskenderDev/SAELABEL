# SAELABEL

Repositorio independiente para el core de etiquetas.

## Proyectos
- `src/SAELABEL.Core`: lógica de parseo/render/ZPL de etiquetas.
- `src/SAELABEL.Api`: API REST en C# para parse/convert/render/print.
- `tests/SAELABEL.Core.Tests`: pruebas unitarias del core.

## Build
```bash
dotnet build SAELABEL.slnx
```

## Test
```bash
dotnet test SAELABEL.slnx
```

## API + OpenAPI + Scalar
```bash
dotnet run --project src/SAELABEL.Api
```

- OpenAPI JSON: `https://localhost:7097/openapi/v1.json`
- Scalar UI: `https://localhost:7097/scalar`

## Integracion frontend (Tauri 2 + React + Astro)
- CORS se configura en `src/SAELABEL.Api/appsettings*.json` en `Cors:AllowedOrigins`.
- El backend acepta por defecto:
  - `http://localhost:1420` (dev Tauri/Vite)
  - `http://localhost:4321` y `https://localhost:4321` (Astro)
  - `tauri://localhost` (WebView Tauri)
