# SAE.STUDIO

Repositorio independiente para el core de etiquetas.

## Proyectos
- `src/SAE.STUDIO.Core`: lógica de parseo/render/ZPL de etiquetas.
- `src/SAE.STUDIO.Api`: API REST en C# para parse/convert/render/print.
- `tests/SAE.STUDIO.Core.Tests`: pruebas unitarias del core.

## Build
```bash
dotnet build SAE.STUDIO.slnx
```

## Test
```bash
dotnet test SAE.STUDIO.slnx
```

## API + OpenAPI + Scalar
```bash
dotnet run --project src/SAE.STUDIO.Api
```

- OpenAPI JSON: `https://localhost:7097/openapi/v1.json`
- Scalar UI: `https://localhost:7097/scalar`

## Integracion frontend (Tauri 2 + React + Astro)
- CORS se configura en `src/SAE.STUDIO.Api/appsettings*.json` en `Cors:AllowedOrigins`.
- El backend acepta por defecto:
  - `http://localhost:1420` (dev Tauri/Vite)
  - `http://localhost:4321` y `https://localhost:4321` (Astro)
  - `tauri://localhost` (WebView Tauri)

## Cliente TypeScript desde OpenAPI
Con la API corriendo, puedes generar cliente tipado para React/Astro:

```bash
npx @hey-api/openapi-ts --input https://localhost:7097/openapi/v1.json --output ./generated/SAE.STUDIO-client
```
