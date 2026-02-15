# NppTests - pokretanje testova

## 1. Sta postoji u NppTests

- `NppApi.ComponentTests/` - NUnit komponentni testovi backend kontrolera
- `Npp.PlaywrightTests/` - Playwright API i E2E testovi

## 2. Preduslovi

- Docker Desktop mora da radi
- Servisi iz `docker/docker-compose.yml` treba da budu podignuti (backend, frontend, cassandra, redis)
- Cassandra schema treba da bude ucitana (`dotnet/NppCore/Db/schema.cql`)

Napomena:
- Ako schema nije ucitana, registracija/login testovi mogu da padaju.

## 2.1 Playwright podesavanja preko `.env` (preporuceno)

`TestSettings.cs` sada ucitava vrednosti ovim redosledom:
1. environment varijable (`-e KEY=VALUE`)
2. `.env` fajl
3. hardcoded default vrednosti

Primer `.env` fajla:

```powershell
copy dotnet\NppTests\Npp.PlaywrightTests\.env.example dotnet\NppTests\Npp.PlaywrightTests\.env
```

Najvaznije promenljive:
- `NPP_PW_SLOWMO_MS` (npr. `300`, `700`, `1000`)
- `NPP_PW_COLOR_SCHEME` (`dark`, `light`, `no-preference`)
- `NPP_PW_HEADLESS` (`true` ili `false`)
- `NPP_FRONTEND_URL`
- `NPP_BACKEND_URL`
- `NPP_PW_VIEWPORT_WIDTH`, `NPP_PW_VIEWPORT_HEIGHT`
- `NPP_TEST_ENV_FILE` (opciono: apsolutna putanja do drugog `.env` fajla)

Podrazumevano:
- `slowMo=300`
- `colorScheme=dark`
- `headless=true`

## 3. Preporuceno pokretanje testova (bez posebnog test image-a)

Pokretanje iz `docker/` foldera.

Komponentni testovi:

```powershell
docker compose exec npp-backend dotnet test /app/NppTests/NppApi.ComponentTests/NppApi.ComponentTests.csproj
```

Playwright API testovi:

```powershell
docker compose exec -e NPP_BACKEND_URL=http://localhost:8080 npp-backend dotnet test /app/NppTests/Npp.PlaywrightTests/Npp.PlaywrightTests.csproj --filter "FullyQualifiedName~Api"
```

Playwright E2E testovi (prvi put):

```powershell
docker compose exec npp-backend dotnet build /app/NppTests/Npp.PlaywrightTests/Npp.PlaywrightTests.csproj
docker compose exec npp-backend pwsh /app/NppTests/Npp.PlaywrightTests/bin/Debug/net10.0/playwright.ps1 install chromium
docker compose exec npp-backend pwsh /app/NppTests/Npp.PlaywrightTests/bin/Debug/net10.0/playwright.ps1 install-deps
docker compose exec -e NPP_FRONTEND_URL=http://npp-frontend:5173 -e NPP_BACKEND_URL=http://npp-backend:8080 npp-backend dotnet test /app/NppTests/Npp.PlaywrightTests/Npp.PlaywrightTests.csproj --filter "FullyQualifiedName~E2E"
```

## 4. Artefakti (screenshot/video)

E2E testovi automatski cuvaju:
- screenshot (`<ImeTesta>_final_state.png`)
- video

Video fajl se po zavrsetku testa preimenuje u:
- `<ImeTesta>_<timestamp>.webm`

Default lokacija:
- `/app/NppTests/Npp.PlaywrightTests/bin/Debug/net10.0/playwright-artifacts/`
- slike: `/app/NppTests/Npp.PlaywrightTests/bin/Debug/net10.0/playwright-artifacts/images/`
- video: `/app/NppTests/Npp.PlaywrightTests/bin/Debug/net10.0/playwright-artifacts/videos/`

Kopiranje sa kontejnera:

```powershell
docker cp npp-backend:/app/NppTests/Npp.PlaywrightTests/bin/Debug/net10.0/playwright-artifacts .\\playwright-artifacts
```

## 5. Brzi troubleshooting

Problem:
- `Keyspace 'npp' does not exist`

Resenje:

```powershell
cd docker
Get-Content ..\dotnet\NppCore\Db\schema.cql | docker compose exec -T cassandra cqlsh
```

Problem:
- E2E test ne moze da otvori frontend/backend URL

Resenje:
- proveri da su `npp-frontend` i `npp-backend` up (`docker compose ps`)
- za E2E koristi `NPP_FRONTEND_URL=http://npp-frontend:5173` i `NPP_BACKEND_URL=http://npp-backend:8080`

Problem:
- `NPP_PW_HEADLESS=false` ne radi u Docker kontejneru

Resenje:
- u kontejneru najcesce koristi `NPP_PW_HEADLESS=true`
- `false` koristi samo na lokalnoj masini koja ima GUI/X server
