# Результаты официальной проверки

Обе версии адаптера прошли **10 из 10 кейсов и 12 из 12 шагов**. Расхождений с эталоном, ошибочных пропусков и эскалаций, сбоев адаптера нет. Неизменённый скрипт создавал отдельный чистый каталог состояния для каждого кейса. [Коммит замеров](https://github.com/PolitovArtyom/invoice-exception-case-study/commit/a6afb58445653180de04d8293fb15e41006152f6)

| Адаптер | Отчёты | Самый долгий шаг |
| --- | --- | --- |
| `dist/linux-x64/InvoiceDesk.Adapter` | [JSON](native/eval-report.json) · [текст](native/eval-report.txt) | 2,918 с |
| `./scripts/adapter-docker` | [JSON](docker/eval-report.json) · [текст](docker/eval-report.txt) | 3,158 с |

Проверка прошла на Linux x86-64 с Docker Engine 29.1.2. Экспорт Linux запущен в контейнере с Python. Скрипт с Docker-адаптером работал с Python на хосте и запускал контейнер без сети для каждого шага. Тайм-аут шага — 30 секунд. Отчёты скопированы из `results/local/exported/` и `results/local/docker fallback/`. При входе `{}` оба адаптера вернули один JSON с `VALIDATION_ERROR` и код выхода 2. [Команды и проверка](../../rehearsal.md#запуск-официальной-проверки)

| Экспорт | Файлы, проверенные на Linux x86-64 |
| --- | --- |
| `linux-x64` | x86-64 ELF и `libe_sqlite3.so`. Адаптер также запускался на этой платформе. |
| `linux-arm64` | AArch64 ELF и `libe_sqlite3.so`. |
| `osx-arm64` | arm64 Mach-O и `libe_sqlite3.dylib`. |
| `osx-x64` | x86-64 Mach-O и `libe_sqlite3.dylib`. |
| `win-x64` | x86-64 PE и `e_sqlite3.dll`. |
