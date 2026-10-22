# Рабочие процессы GitHub Actions

`build.yml` выполняет на `windows-latest`:

1. Получение исходников.
2. `scripts/build.ps1 -Test` — сборку и проверки логики.
3. `scripts/package-release.ps1 -SkipBuild` — подготовку переносимого архива.
4. Загрузку ZIP, контрольной суммы, описания релиза и `artifacts/test-results.txt` в артефакты Actions.

У рабочего процесса только разрешение `contents: read`. Он не создаёт теги и GitHub Releases. Проверку настоящих окон с `-SmokeTest` запускают локально через [скрипт сборки](../../scripts/README.md).
