# NPP - Network Ping Pong

Multiplayer ping pong igra u realnom vremenu sa .NET backend-om i React frontend-om.

## Tehnologije

- **Backend**: .NET 10, ASP.NET Core, SignalR
- **Frontend**: React 19, TypeScript, Vite
- **Baza podataka**: Cassandra (za perzistenciju - planirano)
- **Cache**: Redis (za stanje igre u realnom vremenu - planirano)
- **Kontejnerizacija**: Docker, Docker Compose

## Struktura projekta

```
npp/
├── docker/
│   └── docker-compose.yml    # Docker Compose konfiguracija
├── dotnet/
│   ├── NppApi/               # ASP.NET Core Web API
│   │   ├── Hubs/             # SignalR hub-ovi
│   │   └── Services/         # Servisi (GameManager)
│   └── NppCore/              # Deljeni modeli i logika
│       └── Models/           # Game, Ball, Paddle, Player
└── react/
    └── src/
        ├── components/       # React komponente (Lobby, GameCanvas)
        └── hooks/            # Custom hook-ovi (useGameHub)
```

## Pokretanje

### Preduslovi

- Docker i Docker Compose

### Koraci

1. Klonirajte repozitorijum:
   ```bash
   git clone <repo-url>
   cd npp
   ```

2. Pokrenite aplikaciju:
   ```bash
   cd docker
   docker compose up --build
   ```

3. Otvorite pregledac:
   - Frontend: http://localhost:3000
   - Backend API: http://localhost:5000

## Kako igrati

1. Otvorite http://localhost:3000 u dva browser taba (ili na dva racunara)

2. **Tab 1 - Kreiranje igre:**
   - Unesite vase ime
   - Kliknite "Create Game"
   - Sacekajte protivnika

3. **Tab 2 - Pridruzivanje:**
   - Unesite vase ime
   - U listi otvorenih igara kliknite "Join"

4. **Kontrole:**
   - `W` ili `Strelica Gore` - pomeranje palice gore
   - `S` ili `Strelica Dole` - pomeranje palice dole

5. **Cilj:**
   - Prvi igrac koji postigne 5 poena pobedjuje

## Testiranje

Testovi se pokrecu iskljucivo kroz Docker — nije potrebna nikakva lokalna instalacija .NET SDK-a ni browser-a.

### Preduslovi

- Docker i Docker Compose
- Kreiran `.env` fajl u `docker/` direktorijumu (videti ispod)

### Kreiranje `.env` fajla

```bash
cd docker
echo "Jwt__SecretKey=neki-tajni-kljuc-minimum-32-karaktera-dug" > .env
```

### Pokretanje svih testova (sa buildom)

```bash
cd docker
docker compose --profile test up --build --abort-on-container-exit npp-tests
```

Ova komanda:
1. Podiže infrastrukturu (Cassandra, Redis, API, Frontend)
2. Ceka da svi servisi budu spremni
3. Ucitava Cassandra semu (`schema.cql`)
4. Pokrece redom sve test suite-ove i ispisuje rezultate u konzolu

### Pokretanje testova bez builda (brze, ako je infrastruktura vec pokrenuta)

Ako su kontejneri vec pokrenuti (`docker compose up -d`), pojedinacne suite-ove mozete pokrenuti direktno:

**Komponentni testovi:**
```bash
docker compose exec npp-backend dotnet test /app/NppTests/NppApi.ComponentTests/NppApi.ComponentTests.csproj
```

**Playwright API testovi:**
```bash
docker compose exec -e NPP_BACKEND_URL=http://localhost:8080 npp-backend \
  dotnet test /app/NppTests/Npp.PlaywrightTests/Npp.PlaywrightTests.csproj --filter "FullyQualifiedName~Api"
```

**Playwright E2E testovi** (prvi put zahteva instalaciju Chromium-a u kontejneru):
```bash
docker compose exec npp-backend dotnet build /app/NppTests/Npp.PlaywrightTests/Npp.PlaywrightTests.csproj
docker compose exec npp-backend pwsh /app/NppTests/Npp.PlaywrightTests/bin/Debug/net10.0/playwright.ps1 install chromium
docker compose exec npp-backend pwsh /app/NppTests/Npp.PlaywrightTests/bin/Debug/net10.0/playwright.ps1 install-deps
docker compose exec \
  -e NPP_FRONTEND_URL=http://npp-frontend:5173 \
  -e NPP_BACKEND_URL=http://npp-backend:8080 \
  npp-backend dotnet test /app/NppTests/Npp.PlaywrightTests/Npp.PlaywrightTests.csproj --filter "FullyQualifiedName~E2E"
```

#### Playwright podesavanja (environment varijable)

| Varijabla | Primer vrednosti | Opis |
|---|---|---|
| `NPP_BACKEND_URL` | `http://npp-backend:8080` | URL backend API-ja |
| `NPP_FRONTEND_URL` | `http://npp-frontend:5173` | URL frontend-a (E2E) |
| `NPP_PW_HEADLESS` | `true` / `false` | Headless rezim (u Docker-u uvek `true`) |
| `NPP_PW_SLOWMO_MS` | `0`, `300`, `700` | Usporavanje koraka (ms) |
| `NPP_PW_COLOR_SCHEME` | `dark` / `light` | Tema browser-a |

### Sta se testira

| Suite | Tip | Opis |
|---|---|---|
| **Component tests** | Unit/integration | Testovi kontrolera, servisa i repozitorijuma bez spoljnih zavisnosti |
| **Playwright API tests** | Integration | HTTP testovi svih REST endpointa prema pravom podignutom API-ju |
| **Playwright E2E tests** | End-to-end | Browser testovi korisnickih tokova kroz UI (Chromium) |

### Preuzimanje artefakata (screenshots i video snimci)

Playwright snima screenshot i video svake E2E sesije. Nakon pokretanja testova:

```bash
docker cp npp-tests:/app/NppTests/Npp.PlaywrightTests/bin/Debug/net10.0/playwright-artifacts ./playwright-artifacts
```

Artefakti se nalaze u `docker/playwright-artifacts/`.

### Gasenje infrastrukture

```bash
cd docker
docker compose --profile test down -v
```

Zastavica `-v` uklanja i volumes (Cassandra i Redis podaci), sto je preporuceno izmedju test run-ova kako bi se osiguralo cisto stanje.

---

## Razvoj

### Hot Reload

Oba servisa podrzavaju hot reload u development modu:

- **Backend**: `dotnet watch` automatski restartuje pri promeni .cs fajlova
- **Frontend**: Vite HMR automatski osvezava browser pri promeni

### Pokretanje bez Docker-a

**Backend:**
```bash
cd dotnet/NppApi
dotnet watch run
```

**Frontend:**
```bash
cd react
npm install
npm run dev
```

## Arhitektura

```
┌─────────────┐     SignalR      ┌─────────────┐
│   React     │◄────WebSocket────►│   .NET      │
│   Client    │                   │   Backend   │
├─────────────┤                   ├─────────────┤
│ - Canvas    │                   │ - GameHub   │
│ - Lobby UI  │                   │ - GameState │
│ - SignalR   │                   │ - In-memory │
└─────────────┘                   └─────────────┘
```

- **SignalR** se koristi za real-time komunikaciju izmedju klijenata i servera
- **GameManager** servis upravlja stanjem svih aktivnih igara u memoriji
- **Game loop** radi na 60 FPS i racuna fiziku lopte i kolizije
- Stanje igre se salje svim igracima 60 puta u sekundi
