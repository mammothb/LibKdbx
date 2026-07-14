.PHONY: restore build test pack publish clean coverage coverage-report

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
	rm -rf ./nupkgs ./coverage
	find . -type d \( -name bin -o -name obj \) -exec rm -rf {} + 2>/dev/null || true
	dotnet clean

# --- Coverage ---

COVERAGE_DIR  := ./coverage
COVERAGE_FILE := coverage.cobertura.xml
COVERAGE_XML  := $(COVERAGE_DIR)/$(COVERAGE_FILE)

coverage: build
	mkdir -p $(COVERAGE_DIR)
	dotnet test \
		--configuration Release \
		--no-build \
		--coverage \
		--coverage-output-format cobertura \
		--coverage-output $(COVERAGE_FILE) \
		--results-directory $(COVERAGE_DIR)

coverage-report: coverage
	dotnet reportgenerator \
		-reports:$(COVERAGE_XML) \
		-targetdir:$(COVERAGE_DIR)/html \
		-reporttypes:Html
	dotnet reportgenerator \
		-reports:$(COVERAGE_XML) \
		-targetdir:$(COVERAGE_DIR) \
		-reporttypes:TextSummary
	@echo "Report: $(CURDIR)/$(COVERAGE_DIR)/html/index.html"
