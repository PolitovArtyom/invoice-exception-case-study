# Репетиция изменения правил

Каждое упражнение начиналось с `core-v1` в отдельном временном клоне. До правок записали ожидаемые решения, вопросы и суммы. Сначала выбранные тесты упали на старом поведении. Публичный набор данных, политика, схемы и официальный скрипт проверки остались прежними.

## 1. Увеличить допуск цены с 0.01 до 0.05 включительно

- В [UnitPriceCheck.cs](../src/InvoiceDesk.Core/Rules/Pricing/UnitPriceCheck.cs) задали 0.05. При цене заказа 10.00 [API-тесты](../tests/InvoiceDesk.Tests/Api/InvoiceCalculationTests.cs) и [модульные тесты](../tests/InvoiceDesk.Tests/Rules/InvoiceComparisonTests.cs) пропустили 10.05 и 9.95 и отправили на проверку 10.06 и 9.94. Через API проверили допуск и расчёты. При цене 10.05 ожидаемый НДС — 2.01, итог — 12.06.
- Обновили допуск в [CleanAssessment.cs](../tests/InvoiceDesk.Tests/Support/CleanAssessment.cs). В копии [PublicDiscrepancyTests.cs](../tests/InvoiceDesk.Tests/Api/PublicDiscrepancyTests.cs) тест `Should_clear_within_tolerance` заменил `Should_preserve_price_failure_when_case_004_contains_instruction_text`. Новый тест и [прямой прогон кейсов](../tests/InvoiceDesk.Tests/Scenarios/PublicReplayTests.cs) ожидали `clear` для 004 с тем же PDF и разницей 0.02. Официальный скрипт для опубликованной `core-v1` по-прежнему ожидает `needs_review`.
- В [InvoiceCalculationTests.cs](../tests/InvoiceDesk.Tests/Api/InvoiceCalculationTests.cs), [InvoicePartyTests.cs](../tests/InvoiceDesk.Tests/Api/InvoicePartyTests.cs) и [InvoiceDuplicateTests.cs](../tests/InvoiceDesk.Tests/Api/InvoiceDuplicateTests.cs) задали расхождения за новым пределом и пересчитали суммы.
- Первый полный прогон выявил 12 тестов принятия и исправления, которые использовали разницу 0.02 в 004. В копиях [DiscrepancyAcceptanceTests.cs](../tests/InvoiceDesk.Tests/Api/DiscrepancyAcceptanceTests.cs) и [FactCorrectionTests.cs](../tests/InvoiceDesk.Tests/Api/FactCorrectionTests.cs) установили цену заказа 12.46 при 12.52 в PDF. После этого все тесты прошли.

## 2. Отправить расхождение валют на `needs_review`

- В [CurrencyMatchCheck.cs](../src/InvoiceDesk.Core/Rules/Parties/CurrencyMatchCheck.cs) проверка `CURRENCY_MATCH` при разных валютах стала требовать ручного разбора.
- В [InvoicePartyTests.cs](../tests/InvoiceDesk.Tests/Api/InvoicePartyTests.cs) H-01 ожидал `needs_review` для USD вместо EUR. В H-26 отсутствующий IBAN ещё давал вопрос и итог `needs_information` при двух расхождениях. H-08 по-прежнему спрашивал валюту, когда она отсутствует.
- При правке этих ожиданий на собеседовании переименуйте `Should_block_currency_when_invoice_currency_differs` в `Should_require_review_when_invoice_currency_differs`, а `Should_prioritize_blocking_currency_when_price_review_and_missing_bank_coexist` в `Should_ask_for_bank_account_when_currency_and_price_require_review`.

## 3. Отправить отсутствующий IBAN на ручной разбор

- В [BankAccountCheck.cs](../src/InvoiceDesk.Core/Rules/Bank/BankAccountCheck.cs) `null` даёт расхождение `BANK_ACCOUNT` уровня `needs_review` с пустым значением со стороны счёта. В публичном кейсе 007 неизвестный IBAN по-прежнему блокирует счёт.
- В [InvoiceCalculationTests.cs](../tests/InvoiceDesk.Tests/Api/InvoiceCalculationTests.cs) H-09 и вариант H-26 с ценой и отсутствующим IBAN ожидали `needs_review` без вопросов. В [InvoicePartyTests.cs](../tests/InvoiceDesk.Tests/Api/InvoicePartyTests.cs) H-10 спрашивал только номер поставщика. Вариант H-26 с валютой по-прежнему блокировал счёт, вопросов не было.
- Во временной копии [PublicReplayTests.cs](../tests/InvoiceDesk.Tests/Scenarios/PublicReplayTests.cs) тест `Should_require_review_when_a_runner_iban` заменил `Should_ask_for_the_bank_account_when_a_runner_fact_is_null_without_a_reason` и проверил тот же результат через адаптер.
- На собеседовании переименуйте тест H-09 `Should_ask_for_bank_account_when_iban_is_missing` в `Should_require_review_when_iban_is_missing`. Обновите имена совмещённых тестов по фактическим ожиданиям.

## Проверка на собеседовании

После каждой правки правил и ожиданий запустите все серверные тесты на опубликованной версии `core-v1`. Это помогает найти тесты, которые используют старое поведение, например принятие расхождения в 004. [Время полного прогона](tests/)

```sh
dotnet build InvoiceDesk.slnx -c Release
dotnet test InvoiceDesk.slnx -c Release --no-build
```

При замере учитывались применение готовых правок, форматирование, сборка и тесты. Чтение кода и подготовку правок отдельно не замеряли.

## Проверка в приложении

Измените правило и поднимите [`PolicyVersion`](../src/InvoiceDesk.Core/Model/CaseMetadata.cs), например до `core-v1-interview`. Пересоберите приложение с тем же томом и нажмите **Re-check**. Появится новая ревизия с новой версией правил. Прежняя оценка останется в истории. Правило исполняется из кода. Опубликованный JSON описывает политику.
