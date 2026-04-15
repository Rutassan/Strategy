#!/usr/bin/env bash
set -euo pipefail

ROOT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

dotnet test "$ROOT_DIR/Strategy.Tests/Strategy.Tests.csproj" \
  /p:CollectCoverage=true \
  /p:CoverletOutputFormat=cobertura \
  /p:CoverletOutput="$ROOT_DIR/TestResults/coverage.xml" \
  /p:Threshold=100 \
  /p:ThresholdType=line \
  /p:ThresholdStat=total
