.PHONY: restore build test pack publish clean

restore:
	dotnet restore

build: restore
	dotnet build --configuration Release --no-restore

test: build
	dotnet test --configuration Release --no-build

pack: test
	dotnet pack LibKdbx/LibKdbx.csproj --configuration Release --no-build --output ./nupkgs

publish: pack
	@echo "Package ready: ./nupkgs/"
	@ls -la ./nupkgs/*.nupkg 2>/dev/null || echo "No .nupkg found"

clean:
	rm -rf ./nupkgs
	find . -type d \( -name bin -o -name obj \) -exec rm -rf {} + 2>/dev/null || true
	dotnet clean
