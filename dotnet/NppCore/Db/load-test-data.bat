@echo off
echo Loading test data into Cassandra...
pushd ..\..\docker
docker compose exec -T cassandra cqlsh < ..\dotnet\NppCore\test-podaci.sql
popd
echo Done!
pause
