# CashBeacon

**Messenger-To-RK7 adapter** — многофункциональный бот-прослойка для получения данных из системы RKeeper 7 (через WhiteServer) и отправки их в мессенджеры Telegram и Max.

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)
[![Telegram](https://img.shields.io/badge/Telegram-Bot-26A5E4?logo=telegram)](https://core.telegram.org/bots)
[![Max](https://img.shields.io/badge/Max-Platform-FF6B00)](https://platform-api.max.ru)

---

## ✨ Возможности

- 📱 **Два мессенджера** — Telegram и Max (единый код, расширяемая архитектура)
- 🍽️ **Регистрация ресторанов** — привязка токена WhiteServer к чату
- 📊 **Получение отчётов** — команда `/report <код>` для печатных макетов из RK7
- 🔔 **События в реальном времени** — уведомления об изменении заказов (через вебхуки WhiteServer)
- 📈 **Аналитика** — команды для мониторинга продаж, отказов, времени обслуживания
- 🔐 **Многоботовая поддержка** — несколько ботов в одном экземпляре приложения
- 🗄️ **Собственная аналитическая БД** — SQLite с автоматической проверкой целостности

---

## 🏗️ Архитектура

```CashBeacon/
├── Bots/                     # Клиенты для работы с мессенджерами
│   ├── Telegram/             # Telegram (поллинг + вебхуки)
│   └── Max/                  # Max (поллинг + вебхуки)
├── Core/                     # Бизнес-логика
│   ├── Processor.cs          # Обработка команд и callbacks
│   ├── Database.cs           # Работа с SQLite
│   └── Analytics/            # Аналитические запросы
├── External/                 # Внешние интеграции
│   ├── WhiteServer/          # Клиент для WhiteServer API
│   └── Webhooks/             # Обработчики входящих вебхуков
└── Program.cs                # Точка входа и DI
```

---

## 🤖 Команды бота

```| Команда | Описание | Пример |
|---------|----------|--------|
| `/help` | Показать справку | `/help` |
| `/register <id> [название] <токен>` | Зарегистрировать ресторан | `/register 123 МойРесторан abc123` |
| `/unregister <id>` | Удалить ресторан | `/unregister 123` |
| `/restaurants` | Список подключенных ресторанов | `/restaurants` |
| `/report <код>` | Получить отчёт по коду макета | `/report 42` |
| `/analytics today` | Аналитика за сегодня (если включена) | `/analytics today` |
```

---

## 📦 Используемые технологии

```| Компонент | Технология |
|-----------|------------|
| Фреймворк | ASP.NET Core 8 |
| ORM | Dapper + SQLite |
| Telegram | Telegram.Bot (поллинг + вебхуки) |
| Max API | HTTP-клиент с длительным опросом |
```

---

## 🔧 Конфигурация

### Основные секции `appsettings.json`

```json
{
  "Database": {
    "Path": "CashBeacon.db"          // Путь к БД (по умолчанию в папке приложения)
  },
  "LicenseWarningDays": 3,           // За сколько дней предупреждать об истечении лицензии
  "WhiteServer": { ... },            // Настройки интеграции
  "Telegram": { "Bots": [...] },     // Настройки Telegram-ботов (множественные)
  "Max": { "Bots": [...] }           // Настройки Max-ботов
}
```

### Поддержка нескольких ботов

Каждый бот конфигурируется отдельно:

```json
"Telegram": {
  "Bots": [
    {
      "Name": "Ресторанный бот",
      "Token": "123:abc",
      "Transport": {
        "Mode": "Webhook",
        "WebhookUrl": "https://public-url.com/webhook/telegram",
        "WebhookSecret": "my-secret",
        "Certificate": {
          "Path": "/path/to/cert.pem"
        }
      }
    }
  ]
}
```

---

## 📄 Лицензия

MIT License — подробности в файле [LICENSE](LICENSE).

---

## 📚 Дополнительная документация

- [Документация WhiteServer](https://docs.rkeeper.ru/ws/)
- [Telegram Bot API](https://core.telegram.org/bots/api)
- [Max Platform API](https://platform-api.max.ru/docs)