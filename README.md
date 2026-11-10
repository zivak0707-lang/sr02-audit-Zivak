# sr02-audit-Zivak

Самостійна робота № 2 «Аудит якості та рефакторинг успадкованого коду»,
дисципліна «Конструювання програмного забезпечення».

- Виконав: Зівак Сергій, група 471, варіант 7 (CarRental).
- Джерело коду: А — пакет викладача `V07-CarRental` з репозиторію
  https://github.com/andreichenko256/legacy-code (без змін, тег `baseline`).
- Модуль-донор: `RentService.Close` — розрахунок суми при поверненні авто.
- Профільний дефект: дублювання формули прокату в трьох методах
  (Quote, Close, Extend); обов'язкові рефакторинги — Extract Method, Replace Temp with Query.

## Структура

| Шлях | Що це |
|---|---|
| `src/` | код, що аудитується (консольний застосунок .NET 8) |
| `Legacy.Tests/` | характеризаційні тести (xUnit) |
| `tools/Metrics/` | вимірювач метрик за методикою М1–М3 (Roslyn) |
| `docs/metrics-before.csv`, `docs/metrics-after.csv` | метрики до і після |
| `docs/defects.md` | реєстр дефектів проєктування коду |
| `docs/plan.md` | план рефакторингу «вигода / ризик» |
| `docs/decisions.md` | журнал рішень |
| `docs/debt.md` | реєстр технічного боргу і план погашення |
| `docs/out-before.txt`, `docs/out-after.txt` | вивід демо-сценарію до і після |

Теги: `baseline` — вихідний код; `safety-net` — сітка тестів готова;
`after-refactoring` — усі рефакторинги виконано.

## Як запустити

```
dotnet test
dotnet run --project src
dotnet run --project tools/Metrics -- src docs/metrics-now.csv
```

Покриття: `dotnet test --collect:"XPlat Code Coverage"` (звіт Cobertura в `Legacy.Tests/TestResults`).

Перевірка тих самих тестів до рефакторингу: `git checkout safety-net`, потім `dotnet test`
(кількість Passed однакова з `after-refactoring`; повернутися — `git checkout main`).
