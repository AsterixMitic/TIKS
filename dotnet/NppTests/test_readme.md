# NppTests - pokretanje testova

## 1. Sta postoji u NppTests

- `NppApi.ComponentTests/` - NUnit komponentni testovi (kontroleri + servisi)
- `Npp.PlaywrightTests/` - Playwright API i E2E testovi

## 2. Preduslovi

- Docker Desktop mora da radi
- `.env` fajl u `docker/` folderu (videti `docker/.env.example`)

## 3. Automatsko pokretanje svih testova (PREPORUCENO)

Jedna komanda pokrece celu infrastrukturu, ucitava semu i pokrece sve testove:

```powershell
cd docker
docker compose --profile test up --build --abort-on-container-exit npp-tests
```

Ovo automatski:
1. Pokrece Cassandra, Redis, backend i frontend
2. Ceka da Cassandra bude zdrava
3. Ucitava Cassandra semu (`schema.cql`)
4. Ceka da backend bude spreman
5. Pokrece komponentne testove (unit)
6. Pokrece Playwright API testove
7. Pokrece Playwright E2E testove (Chromium je vec instaliran u image-u)

Zaustavljanje posle testova:

```powershell
docker compose --profile test down -v
```

## 4. Rucno pokretanje pojedinacnih testova

Ako vec imate infrastrukturu pokrenutu (`docker compose up -d`):

Komponentni testovi:

```powershell
docker compose exec npp-backend dotnet test /app/NppTests/NppApi.ComponentTests/NppApi.ComponentTests.csproj
```

Playwright API testovi:

```powershell
docker compose exec -e NPP_BACKEND_URL=http://localhost:8080 npp-backend dotnet test /app/NppTests/Npp.PlaywrightTests/Npp.PlaywrightTests.csproj --filter "FullyQualifiedName~Api"
```

Playwright E2E testovi (prvi put zahteva instalaciju Chromium-a):

```powershell
docker compose exec npp-backend dotnet build /app/NppTests/Npp.PlaywrightTests/Npp.PlaywrightTests.csproj
docker compose exec npp-backend pwsh /app/NppTests/Npp.PlaywrightTests/bin/Debug/net10.0/playwright.ps1 install chromium
docker compose exec npp-backend pwsh /app/NppTests/Npp.PlaywrightTests/bin/Debug/net10.0/playwright.ps1 install-deps
docker compose exec -e NPP_FRONTEND_URL=http://npp-frontend:5173 -e NPP_BACKEND_URL=http://npp-backend:8080 npp-backend dotnet test /app/NppTests/Npp.PlaywrightTests/Npp.PlaywrightTests.csproj --filter "FullyQualifiedName~E2E"
```

## 4.1 Playwright podesavanja preko `.env`

`TestSettings.cs` ucitava vrednosti ovim redosledom:
1. environment varijable (`-e KEY=VALUE`)
2. `playwright.settings.json` fajl
3. hardcoded default vrednosti

Najvaznije promenljive:
- `NPP_PW_SLOWMO_MS` (npr. `300`, `700`, `1000`)
- `NPP_PW_COLOR_SCHEME` (`dark`, `light`, `no-preference`)
- `NPP_PW_HEADLESS` (`true` ili `false`)
- `NPP_FRONTEND_URL`
- `NPP_BACKEND_URL`
- `NPP_PW_VIEWPORT_WIDTH`, `NPP_PW_VIEWPORT_HEIGHT`

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
