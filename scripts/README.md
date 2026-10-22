# Сборка и упаковка

- `build.ps1` — собирает все `.cs` из `src/` и `tests/SelfTests.cs` компилятором .NET Framework, добавляет иконку и манифест, копирует конфигурацию .NET.
- `package-release.ps1` — собирает и проверяет приложение, затем создаёт переносимый ZIP, SHA-256 и описание релиза.

Команды из корня репозитория:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\build.ps1 -Test -SmokeTest
powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\package-release.ps1 -SkipBuild
```

Ключи сборки: `-Test` запускает проверки логики, `-SmokeTest` — проверку настоящих окон. Без ключей выполняется только сборка. `-SkipBuild` при упаковке использует уже собранную программу; без него упаковщик сначала выполняет сборку с `-Test`.

Скрипты работают в Windows PowerShell 5.1 и PowerShell 7, определяют корень по собственному пути и не зависят от текущей папки. Файлы `.ps1` сохраняются как UTF-8 с BOM для корректной работы кириллицы в Windows PowerShell.

Результат сборки — [bin](../bin/README.md), отчёты — [artifacts](../artifacts/README.md), файлы релиза — [dist](../dist/README.md). Перед пересборкой закрой запущенную копию из `bin/`. Для повторной упаковки той же версии также закрой программу, если она запущена из её вложенной папки в `dist/`: упаковщик обновляет файлы этой папки.
