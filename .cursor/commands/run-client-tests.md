# run-client-tests

Run client tests with these commands

# Test All Run lint, build, unit tests, and E2E tests
```
Set-Location tenxempires.client
Write-Host "Running lint..." -ForegroundColor Cyan
```

## Lint
```
npm run lint
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

## Build
```
Write-Host "Running build..." -ForegroundColor Cyan
npm run build 
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

## Unit tests
Write-Host "Running unit tests..." -ForegroundColor Cyan
npm test
if ($LASTEXITCODE -ne 0) { exit $LASTEXITCODE }
```

## E2E tests
```
Write-Host "Running E2E tests..." -ForegroundColor Cyan
npm run test:e2e
```

If the homepage changes intentionally, update the baseline by running:
```
npx playwright test e2e/example.spec.ts -g "homepage should match screenshot" --update-snapshots
```

