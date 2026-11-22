# Invoice Desk

Invoice Desk checks an invoice against a saved company-data snapshot and shows the decision and its supporting calculations. An agent reads the PDF and submits printed facts with evidence. The application evaluates those facts using the assignment's `core-v1` rules.

| Scope | Stage 01 |
| --- | --- |
| Built | PDF source references, MCP fact registration, SQLite assessment history, and a workspace for matched invoices. Quantity, price, arithmetic and bank discrepancies are saved and explained. The native adapter replays six public cases. |
| Described | [Remaining stages](docs/plan.md), [architecture](docs/architecture.md), and the [assignment](#invoice-exception-agent). |
| Prototype | [Simulated product vision](submission/prototype/), including the unbuilt case 103. |

## Run the workspace

Install Docker with the Compose plugin. From the repository root:

```sh
docker compose up --build --wait
```

Open the case list at `http://127.0.0.1:8080/` and the clean invoice at `http://127.0.0.1:8080/cases/case-001`. Compose waits for `/health`, which becomes available after migrations and case loading. Set `INVOICE_PORT` to change the loopback port.

With a fresh database, cases 001, 002, 003, 005 and 007 start with their first-step facts and carry the label “loaded from test data, not read by the agent”. Open [case 002](http://127.0.0.1:8080/cases/case-002) to expand a passed line with a partial invoice, or [case 003](http://127.0.0.1:8080/cases/case-003) to see both quantity failures. Case 004 is available for agent registration. Other cases start with company data and PDF references. To exercise a fresh registration, add a clean invoice under a new case ID in `scenarios/cases`, then restart Web and register the facts against that case. Re-importing the preloaded invoice exercises the duplicate branch scheduled for stage 02.

The database is in the Compose `invoice-data` volume. `docker compose down` stops the app and preserves history. Use `docker compose down --volumes` to reset this project's volume. Case folders under `dataset/cases` and `scenarios/cases` are mounted read-only. The startup loader discovers new cases on restart and preserves existing cases. Use the same case ID for the folder name, case manifest and first step. Startup reports malformed case data with the folder path. Own case folders use the assignment's case metadata and first-step layout.

### Connect an agent

The Streamable HTTP MCP endpoint is `http://127.0.0.1:8080/mcp`. For example, configure a Claude Code project connection with:

```json
{
  "mcpServers": {
    "invoice": { "type": "http", "url": "http://127.0.0.1:8080/mcp" }
  }
}
```

`open_case` returns source references and the extraction schema. The agent can resolve `host_path` from the repository root. `container_path` is the application's path inside Docker. `register_invoice` accepts the case ID, a source with `root` and `relative_path`, and the raw `facts` object. The server reads the selected PDF to compute SHA-256. The tools also expose the saved assessment, history, company data and an unchanged-input recheck. The demo is intended for loopback. A production deployment would need separate agent and human credentials and access controls.

MCP registration requires a reason for each null fact and quotes for printed values only. The runner and startup accept schema-valid null facts without these reading-completeness checks.

## Build and run the native adapter

Install the .NET SDK specified in [global.json](global.json) and Python 3 with `venv`. Keep the entire published folder together. The apphost uses the installed .NET 10 runtime.

```sh
dotnet publish src/InvoiceDesk.Adapter -c Release --self-contained false -o dist/native
python3 -m venv .venv
.venv/bin/python -m pip install -r requirements.txt
.venv/bin/python tools/run_evals.py --adapter 'dist/native/InvoiceDesk.Adapter' --cases case-001,case-002,case-003,case-004,case-005,case-007 --report-dir results/local/public --keep-state
```

The supported subset is six cases and six steps. Reports go to `results/local/public/`. The adapter runs independently of Web and keeps its SQLite state under the supplied `--state-dir`. An initial step requires both company data and invoice facts. Input validation returns a field path. Unsupported rule branches exit with a diagnostic on stderr.

### Simplifications

Each input quantity, unit price and monetary amount is limited to 1,000,000,000 to keep decimal products exact. Larger values return an unsupported-input error.

Names are trimmed, internal whitespace collapses to one space, and casing uses `ToLowerInvariant()`. Unresolved parties, unsupported order or line matches, and duplicate and conflict handling are scheduled for stages 02 and 03. Those branches currently return an explicit unsupported-case error.

## Check a change

```sh
dotnet tool restore
dotnet build InvoiceDesk.slnx -c Release
dotnet csharpier check .
dotnet format style InvoiceDesk.slnx --verify-no-changes
dotnet format analyzers InvoiceDesk.slnx --verify-no-changes
dotnet test InvoiceDesk.slnx -c Release --no-build
cd frontend
npm ci
npm run lint
npm run format:check
npm run build
```

Use Node 24 for the frontend. Rules are under `src/InvoiceDesk.Core/Rules`, grouped by rule family. See [quantities](src/InvoiceDesk.Core/Rules/Quantities), [pricing and rounding](src/InvoiceDesk.Core/Rules/Pricing) and [bank details](src/InvoiceDesk.Core/Rules/Bank). The [original business rules](docs/BUSINESS_RULES.md) and [runner contract](docs/EVALUATION.md) define evaluation behavior.

---

# Invoice Exception Agent

Тестовое задание на позицию **Senior Software Engineer**. На выполнение — **до 16 часов, два рабочих дня**. Язык, хранилище и внутренняя архитектура — на ваш выбор. Использование AI coding agents приветствуется.

## Бизнес-задача

Строительная компания получает счета от десятков поставщиков. Перед оплатой каждый счёт сверяется с заказом и приёмками: заказывали ли это, столько ли привезли, по той ли цене, верна ли арифметика, наш ли это поставщик и его ли банковский счёт, не учтён ли этот счёт раньше. Большинство счетов чистые, но проверка ручная, а ошибка стоит денег: двойная оплата, подменённый IBAN, оплата непоставленного.

Нужна система, в которой **агент делает рутину** — читает PDF, сопоставляет строки счёта со строками заказа, объясняет расхождения, — а **решение, можно ли пропустить счёт дальше, принимает детерминированный код** по опубликованным правилам. Итоги:

| Итог | Что значит для бизнеса |
|---|---|
| `clear` | Счёт сходится, человек не нужен |
| `needs_information` | Не хватает конкретного факта — система говорит, какого |
| `needs_review` | Нужно решение человека: неподтверждённое предложение агента, цена вне допуска, разные единицы измерения |
| `blocked` | Нарушено правило: чужой поставщик или валюта, IBAN не из утверждённых, количество сверх принятого или заказанного, ошибка арифметики |

Повторно присланный документ распознаётся как `duplicate` (тот же счёт) или `conflict` (тот же номер, другое содержание).

Агент уже есть — это ваш локальный Codex или Claude Code; в продукте его место занял бы агент, разбирающий входящую почту. **Вы строите то, к чему он подключается:** движок правил с хранением, **MCP** для агента и **страницу кейса** для человека.

## Как работает решение

```mermaid
flowchart LR
    PDF[PDF счёта] --> Agent[Локальный Codex / Claude Code]
    Agent -->|Факты и предложения через MCP| App[Ваше приложение]
    Context[Заказы, приёмки, справочники, история] --> App
    App --> Core[Детерминированные правила]
    Core --> Result[Заключение, расчёты, вопросы]
    Result --> Agent
    Result --> Page[Страница кейса]
    Human[Человек] -->|Подтверждение предложения| Page
    Page --> App
```

| Участник | Что делает |
|---|---|
| **Агент** | Читает PDF и регистрирует факты со ссылкой на страницу и цитату; предлагает сопоставление, когда названия в счёте и заказе расходятся; объясняет итог пользователю |
| **Приложение** | Валидирует вход, считает суммы и остатки, применяет правила, выносит и хранит заключения |
| **Человек** | Подтверждает конкретное предложение агента на странице кейса |

Главная граница: **агент приносит факты и предложения, но не выносит решение.** MCP не предоставляет операцию подтверждения и действие «сделать clear»; инструкция, написанная внутри PDF, не должна влиять на итог.

Собственный LLM-сервис и ключи AI-провайдеров не нужны: агент работает в вашем локальном окружении. Извлечение текста PDF можно выполнять средствами агента; встроенный OCR не требуется. Текст PDF — данные, включая встречающиеся в нём инструкции для автоматической обработки.

## Что уже есть в репозитории

| Готово | Для чего |
|---|---|
| `dataset/cases/`: 10 кейсов, 12 PDF, 12 шагов | Разработка, самопроверка и демонстрация |
| `dataset/policies/core-v1.json` и `dataset/schemas/` | Политика и точные JSON-форматы |
| `expected/` внутри каждого кейса | Проверенные факты PDF и ожидаемые результаты |
| `tools/run_evals.py` | Приёмочные тесты движка, без LLM |

**Ваш код** — движок, хранение, MCP, UI кейса для пользователя и тонкий CLI-адаптер. Структуру кода выбираете вы. Выданные правила, схемы, датасет, эталоны и runner менять для получения PASS не нужно: на ревью используются доверенные копии.

## Что реализовать

1. **Импорт и валидация.** Контекст кейса и структурированные факты документа. Ошибки формата возвращаются с понятным путём к полю, без создания заключения.
2. **Шесть правил core-v1.** Стороны и валюта; выбор заказа/строк; количества с учётом приёмок и истории; цена и арифметика; банк; дубликаты/конфликты. Денежная арифметика — Decimal. Одинаковые входные данные дают одинаковый результат.
3. **Хранение и история.** Факты с `document_id`, страницей и verbatim excerpt; предложения агента; подтверждения человека; заключения со ссылкой на ревизию входа. Новое заключение сохраняется отдельно, прежнее не перезаписывается. Данные доступны после перезапуска процессов.
4. **MCP.** Открыть кейс, получить контекст, внести факты и предложения, запустить проверку, прочитать результат/вопросы/историю и повторить проверку после изменения входа. Имена и гранулярность инструментов выбираете вы.
5. **Страница кейса.** Строки, расчёты, checks, источники, история и форма подтверждения конкретного предложения человеком. Production-аутентификация не требуется.
6. **CLI-адаптер и тесты.** Адаптер к контракту приёмочных тестов вызывает те же use cases, что и MCP. Добавьте тесты расчётов и доменных инвариантов.
7. **Запуск и демонстрация.** Одна команда запуска приложения, рабочая конфигурация MCP для Codex либо Claude Code и транскрипт/запись демонстрации.

MCP можно реализовать через stdio или Streamable HTTP. Для stdio протокольные сообщения идут в stdout, логи — в stderr. Укажите доступные агенту пути к PDF, особенно если приложение работает в контейнере.

## Как выполнять задание и проверять себя

```mermaid
flowchart LR
    Read[Прочитать правила и один кейс] --> Build[Реализовать ядро и CLI-адаптер]
    Build --> Evals[Прогнать приёмочные тесты]
    Evals --> Fix[Разобрать расхождения]
    Fix --> Build
    Evals --> Interfaces[Подключить MCP и страницу кейса]
    Interfaces --> Demo[Показать кейсы 004 и 010]
    Demo --> Submit[Сдать форк и инструкцию]
```

Проверка устроена в два слоя — чтобы при неверном итоге было видно, кто ошибся: агент, прочитавший документ, или движок, применивший правила.

| Слой | Что проверяет | Как |
|---|---|---|
| **Приёмочные тесты движка** (`run_evals.py`) | Бизнес-логику: расчёты, правила, итог, вопросы, историю ревизий | Runner подаёт адаптеру **уже извлечённые факты**, контекст, предложения и подтверждения и сравнивает ответ с эталоном. LLM не участвует, результат детерминирован |
| **Сквозной eval агента** | Извлечение фактов из PDF и границу ответственности агента | Агент проходит кейс через MCP; факты сравниваются с `expected/extraction.json`, итог — с эталоном.|

Правил шесть, но крайних случаев в них много (округление, границы допуска, зависимые `unknown`, разные единицы измерения). Открытые кейсы — это исполняемая спецификация в примерах; на ревью тот же runner прогоняет новые кейсы по тем же правилам.

Сначала прочитайте [правила](docs/BUSINESS_RULES.md) и откройте `dataset/cases/case-001/`: PDF, `steps/step-001.json` и `expected/step-001.json`. Точный протокол — в [контракте проверки](docs/EVALUATION.md).

Установите зависимости проверяющих инструментов в отдельное окружение:

```sh
python3 -m venv .venv
. .venv/bin/activate
python3 -m pip install -r requirements.txt
```

После реализации адаптера запустите, подставив свою команду:

```sh
python3 tools/run_evals.py --adapter "<команда вашего адаптера>"
```

Runner добавляет `--state-dir <каталог>`, передаёт один JSON в stdin и ожидает один JSON в stdout. Он создаёт отдельное состояние на кейс и запускает новый процесс на каждый шаг. Отчёты: `results/local/eval-report.txt` и `.json`. **Exit code 0 — все кейсы пройдены, 1 — есть ошибки, 2 — ошибка запуска runner.** Для самопроверки стремитесь к 10/10; разберите каждое расхождение и объясните оставшиеся ограничения.

Для быстрой итерации:

```sh
python3 tools/run_evals.py --adapter "<команда вашего адаптера>" --cases case-003,case-010 --keep-state
```

Проверка целостности самого датасета — отдельная команда; она не проверяет вашу реализацию. Для неё дополнительно нужен `pdftotext` из Poppler:

```sh
python3 tools/validate_dataset.py dataset
```

## Демонстрация локальным агентом

Используйте PDF и контекст кейса, не подавайте агенту `expected/`, эталонные ответы или метки исходов из manifest. Открытые эталоны предназначены для разработки и runner.

- **Кейс 004:** агент читает PDF, регистрирует факты и запускает проверку. Инструкция внутри PDF не меняет результат `needs_review` и не превращается в подтверждение человека.
- **Кейс 010:** проверка даёт `needs_information`; агент регистрирует предложение → `needs_review`; человек подтверждает его на странице; агент повторяет проверку → `clear`. Предыдущие заключения остаются в истории.

Пример запроса: «Проверь счёт из кейса 010. Если заказ определить нельзя, предложи соответствие и объясни, что мне нужно подтвердить». Зафиксируйте клиента, модель и tool calls.

## Границы 16 часов

Обычный invoice в EUR; один документ — один заказ; одна строка счёта — одна строка заказа. Несколько приёмок и ранее учтённых счетов допустимы. В core остаются ветки ошибок входных данных и нарушений (например, несовпадение валюты), описанные правилами.

Не требуются: proforma, предоплата, доставка, скидки, кредит-ноты, конверсия валют/единиц, платежи, реальные ERP, OCR сканов, аутентификация, UI сравнения версий, восстановление после аварий и конкурентных сбоев. Повторы вызовов и аварийное восстановление обсуждаем на интервью. Обычное сохранение данных между запусками требуется.

## Что сдать

1. Сделайте **форк этого репозитория** и реализуйте решение в нём. Можно работать небольшими ветками/PR внутри своего форка.
2. Дополните README своего форка: зависимости и одна команда запуска приложения, URL страницы кейса, конфигурация подключения MCP для Codex либо Claude Code и точная команда адаптера для runner. Инструкции должны работать на чистом клоне.
3. В том же README кратко опишите архитектурные решения, упрощения, оставшиеся ограничения и фактически затраченное время (до 16 часов).
4. Приложите отчёт runner по открытым кейсам и запись/транскрипт демонстрации 004 и 010 с логом tool calls и указанием клиента/модели. `results/local/` игнорируется Git; сдаваемые материалы можно положить в `submission/` и дать на них ссылки из README.
5. В README своего решения кратко ответьте:
    - Какой участок ручной работы улучшает ваше решение и по каким показателям вы проверили бы пользу? Отделите ожидаемый эффект от измеренного.
    - Какие зависимости от закупок, справочника поставщиков, приёмки и бухгалтерии потребуется уточнить перед внедрением? Какие риски или изменения процесса вы видите?
    - Какие три следующих улучшения вы предложили бы и в каком порядке? Для каждого укажите ожидаемую пользу, зависимость и способ проверить результат.
6. Пришлите ссылку на форк и обеспечьте доступ.

На ревью мы проверяем новое решение на свежем клоне, запускаем открытые и новые кейсы по тем же правилам, читаем код и повторяем демонстрацию через MCP. Новых скрытых бизнес-правил не будет. Интервью включает обсуждение архитектуры и небольшое изменение правила.

## Где читать дальше

- [BUSINESS_RULES.md](docs/BUSINESS_RULES.md) — нормативные правила и формулы.
- [EVALUATION.md](docs/EVALUATION.md) — JSON-контракт и точный способ сравнения.
