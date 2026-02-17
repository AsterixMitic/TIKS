#!/bin/bash
set -e

# Colors for output
GREEN='\033[0;32m'
RED='\033[0;31m'
YELLOW='\033[1;33m'
NC='\033[0m' # No Color

CASSANDRA_HOST="${CASSANDRA_HOST:-cassandra}"
BACKEND_URL="${NPP_BACKEND_URL:-http://npp-backend:8080}"

echo -e "${YELLOW}========================================${NC}"
echo -e "${YELLOW}  NPP Test Runner${NC}"
echo -e "${YELLOW}========================================${NC}"

# -----------------------------------------------
# 1. Wait for Cassandra
# -----------------------------------------------
echo -e "\n${YELLOW}[1/6] Waiting for Cassandra...${NC}"
for i in $(seq 1 30); do
    if cqlsh "$CASSANDRA_HOST" -e "describe cluster" > /dev/null 2>&1; then
        echo -e "${GREEN}Cassandra is ready!${NC}"
        break
    fi
    if [ "$i" -eq 30 ]; then
        echo -e "${RED}Cassandra did not become ready in time.${NC}"
        exit 1
    fi
    echo "  Attempt $i/30 - waiting 10s..."
    sleep 10
done

# -----------------------------------------------
# 2. Load schema (idempotent - uses IF NOT EXISTS)
# -----------------------------------------------
echo -e "\n${YELLOW}[2/6] Loading Cassandra schema...${NC}"
cqlsh "$CASSANDRA_HOST" -f /app/NppCore/Db/schema.cql
echo -e "${GREEN}Schema loaded.${NC}"

# -----------------------------------------------
# 3. Wait for backend
# -----------------------------------------------
echo -e "\n${YELLOW}[3/6] Waiting for backend at ${BACKEND_URL}...${NC}"
for i in $(seq 1 30); do
    if curl -sf "${BACKEND_URL}/WeatherForecast" > /dev/null 2>&1; then
        echo -e "${GREEN}Backend is ready!${NC}"
        break
    fi
    if [ "$i" -eq 30 ]; then
        echo -e "${RED}Backend did not become ready in time.${NC}"
        exit 1
    fi
    echo "  Attempt $i/30 - waiting 5s..."
    sleep 5
done

# Clean up previous Playwright artifacts
ARTIFACTS_DIR="/app/NppTests/Npp.PlaywrightTests/bin/Debug/net10.0/playwright-artifacts"
if [ -d "$ARTIFACTS_DIR" ]; then
    echo -e "\n${YELLOW}Cleaning previous Playwright artifacts...${NC}"
    rm -rf "$ARTIFACTS_DIR"
fi

# Track overall result
RESULT=0

# -----------------------------------------------
# 4. Run Component tests
# -----------------------------------------------
echo -e "\n${YELLOW}[4/6] Running Component tests...${NC}"
if dotnet test /app/NppTests/NppApi.ComponentTests/NppApi.ComponentTests.csproj \
    --no-build --logger "console;verbosity=normal"; then
    echo -e "${GREEN}Component tests PASSED${NC}"
else
    echo -e "${RED}Component tests FAILED${NC}"
    RESULT=1
fi

# -----------------------------------------------
# 5. Run Playwright API tests
# -----------------------------------------------
echo -e "\n${YELLOW}[5/6] Running Playwright API tests...${NC}"
if dotnet test /app/NppTests/Npp.PlaywrightTests/Npp.PlaywrightTests.csproj \
    --no-build --filter "FullyQualifiedName~Api" --logger "console;verbosity=normal"; then
    echo -e "${GREEN}API tests PASSED${NC}"
else
    echo -e "${RED}API tests FAILED${NC}"
    RESULT=1
fi

# -----------------------------------------------
# 6. Run Playwright E2E tests
# -----------------------------------------------
echo -e "\n${YELLOW}[6/6] Running Playwright E2E tests...${NC}"
if dotnet test /app/NppTests/Npp.PlaywrightTests/Npp.PlaywrightTests.csproj \
    --no-build --filter "FullyQualifiedName~E2E" --logger "console;verbosity=normal"; then
    echo -e "${GREEN}E2E tests PASSED${NC}"
else
    echo -e "${RED}E2E tests FAILED${NC}"
    RESULT=1
fi

# -----------------------------------------------
# Summary
# -----------------------------------------------
echo -e "\n${YELLOW}========================================${NC}"
if [ $RESULT -eq 0 ]; then
    echo -e "${GREEN}  ALL TESTS PASSED${NC}"
else
    echo -e "${RED}  SOME TESTS FAILED${NC}"
fi
echo -e "${YELLOW}========================================${NC}"

exit $RESULT
