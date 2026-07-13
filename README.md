# CashBeacon

**CashBeacon** — бот для Telegram и Max, который через WhiteServer показывает данные из RK7 (отчёты, события заказов и другое).

[![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?logo=dotnet)](https://dotnet.microsoft.com/)
[![License](https://img.shields.io/badge/License-MIT-green.svg)](LICENSE)

---

## 🚀 Возможности

- Telegram + Max (единый код, расширяется добавлением модулей)
- Получение отчётов — команда `/report <код>` для получения отчёта из RK7
- События заказов в реальном времени (подписка на события от WhiteServer)
- Несколько ботов в одном запущенном экземпляре

---

## 🏗️ Структура

```text
CashBeacon/
├── Bots/          # Клиенты для мессенджеров
├── Core/          # Бизнес-логика
├── Data/          # БД
├── External/      # WhiteServer
└── Program.cs     # Точка входа
```

---

## ⚡ Быстрый старт

```bash
git clone https://github.com/yourusername/CashBeacon.git
cd CashBeacon
dotnet restore