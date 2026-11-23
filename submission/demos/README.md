# Демонстрации в Claude Code

[Скрипт запуска](run-claude) записывает поток JSON. В каждой сессии Claude Code 2.1.330 выбрал модель `claude-sonnet-5-5`. Доступны Read и семь инструментов MCP для счетов. [Настройки доступа](settings.json) ограничивают чтение рабочей папкой, в которой лежит только копия PDF. У агента нет инструментов для консоли, запроса к веб-странице, правок или подтверждения предложения. Сохранение сессий и команды со слешем отключены. [Системный запрос](system-prompt.txt)

Кейсы 004 и 010 записаны на Stage 07. Видеозаписи показывают действия в карточке, включая принятие расхождения в 004 и подтверждение сопоставления в 010. [Проверка на чистом клоне](../rehearsal.md)

Для каждого кейса создаётся отдельная папка вне репозитория. Агент получает запрос, PDF и ответы MCP. Приложение подаёт данные компании, хранит состояние в томе Docker и монтирует файлы кейсов только для чтения. У агента нет доступа к входам и эталонам официальной проверки.

Из корня репозитория запустите приложение с чистым томом и скопируйте PDF. Скрипты используют `INVOICE_BASE_URL` со значением по умолчанию `http://127.0.0.1:8080`. Если задаёте другой `INVOICE_PORT`, экспортируйте соответствующий `INVOICE_BASE_URL` перед обоими запусками.

```bash
docker compose -p invoice-desk-demo up --build --wait
demo_root=$(mktemp -d)
mkdir "$demo_root"/{004,010,claude}
cp dataset/cases/case-004/documents/document-001.pdf "$demo_root/004/"
cp dataset/cases/case-010/documents/document-001.pdf "$demo_root/010/"
```

Временной папке `claude` нужна действующая авторизация Claude. В записи использовалась изолированная копия авторизации клиента. Вызовы для двух кейсов:

```bash
for case_id in 004 010; do
  env -u CLAUDECODE -u CLAUDE_CODE_ENTRYPOINT CLAUDE_CONFIG_DIR="$demo_root/claude" \
    submission/demos/run-claude "$demo_root/$case_id" "$case_id/prompt.txt" \
    > "$demo_root/$case_id.jsonl"
done
```

Для подтверждения на странице 010 запустите [скрипт Playwright](010/confirm.mjs) из временной установки. Он сохранит видео и снимки в `demo_root`.

```bash
npm install --prefix "$demo_root/browser" playwright@1.58.2
"$demo_root/browser/node_modules/.bin/playwright" install firefox
cp submission/demos/010/confirm.mjs "$demo_root/browser/"
node "$demo_root/browser/confirm.mjs" "$demo_root"
```

Затем запустите новую сессию для повторной проверки:

```bash
env -u CLAUDECODE -u CLAUDE_CODE_ENTRYPOINT CLAUDE_CONFIG_DIR="$demo_root/claude" \
  submission/demos/run-claude "$demo_root/010" 010/after-confirmation-prompt.txt \
  > "$demo_root/010-after.jsonl"
```

В опубликованных записях сохранены модель, инструменты, аргументы, ответы и содержимое PDF. Из записей убраны `rate_limit_event`, `credential_expires_at`, `apiKeySource`, `request_id` и локальные списки агентов и расширений. Путь временной рабочей папки заменён на `<isolated-demo>`. Данные кейсов синтетические.

- [004 · Указание внутри PDF](004/)
- [010 · Предложение агента и подтверждение в браузере](010/)
- [Сверка извлечённых данных и оценок](../extraction/)
