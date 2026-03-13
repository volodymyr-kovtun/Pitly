# Pitly

Polish tax declaration assistant for **Interactive Brokers** users based in Poland. Automates the most painful parts of filing PIT-38: parsing your IB Activity Statement CSV, converting all USD/EUR amounts to PLN using official NBP exchange rates, calculating capital gains with FIFO, computing dividend tax with foreign withholding credit, and producing the exact field values you need to enter in your PIT-38 form.

> **Note:** Currently only **Interactive Brokers (IB)** is supported. Support for more brokers may be added in the future.

## Why this exists

If you invest through Interactive Brokers from Poland, every year by April 30 you must file PIT-38. This requires:

1. Converting every transaction to PLN using the NBP mid rate from the **last business day before** the transaction date
2. Calculating capital gains using the **FIFO method** (first in, first out) across all sell transactions
3. Declaring foreign dividends and offsetting the foreign withholding tax (typically 15% from the US) against the Polish 19% rate
4. Filling in specific fields (C.20-C.24, D.25-D.26, E.27-E.28) of the PIT-38 form

Doing this manually for dozens of transactions is tedious and error-prone. Pitly does it in seconds.

## Prerequisites

- [.NET 10 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)

## Quick Start

Start both services in separate terminals:

```bash
# Terminal 1 — Backend API
cd src/Pitly.Api
dotnet run --launch-profile https
# Runs on https://localhost:7001

# Terminal 2 — Frontend
cd frontend
npm install
npm run dev
# Runs on http://localhost:5173
```

Open `http://localhost:5173` in your browser.

## How to export your IB Activity Statement

1. Log in to [IB Client Portal](https://www.interactivebrokers.com/sso/Login)
2. Go to **Performance & Reports > Statements**
3. Click **Activity** statement
4. Set the period to the full tax year (e.g. January 1 - December 31, 2024)
5. Select format: **CSV**
6. Download the file and upload it in the app

## What the app does

### 1. Parses your broker CSV

The parser reads three sections from the IB Activity Statement:

- **Trades** — buys and sells of stocks (fractional shares supported)
- **Dividends** — cash dividend payments, with symbol and date extracted from the description
- **Withholding Tax** — foreign tax withheld on dividends, matched to the corresponding dividend by symbol and date

Forex trades, futures, options, and other asset categories are ignored.

### 2. Fetches NBP exchange rates

For each transaction date, the app calls the [NBP API](https://api.nbp.pl/) to get the official mid exchange rate from Table A. Per Polish tax law, the rate used is from the **last business day before** the transaction date. If that day falls on a weekend or holiday (NBP returns 404), the app automatically retries up to 5 previous days. Rates are cached in memory to avoid repeated API calls.

### 3. Calculates capital gains (FIFO)

For each symbol, all trades are sorted chronologically. Buy lots are queued. When a sell occurs, lots are dequeued FIFO to determine the cost basis in PLN:

- **Proceeds (PLN)** = sell quantity x price x NBP rate - commission x NBP rate
- **Cost (PLN)** = buy quantity x price x NBP rate + commission x NBP rate (from the matching buy lots)
- **Gain/Loss** = proceeds - cost

Net capital gains across all symbols are taxed at 19%. If the total is a loss, tax owed is 0 (the loss can be carried forward to future years on your own).

### 4. Calculates dividend tax

For each dividend:

- **Dividend (PLN)** = amount x NBP rate
- **Polish tax** = dividend (PLN) x 19%
- **Foreign withholding credit** = min(withholding in PLN, Polish tax)
- **Net tax owed** = Polish tax - credit

Under the PL-US tax treaty, US withholding is typically 15%, so the net Polish tax on dividends is usually ~4%.

### 5. Generates PIT-38 field values

The PIT-38 Guide page shows the exact values for each field:

| Field | Name | Description |
|-------|------|-------------|
| C.20 | Przychody | Total proceeds from selling securities (PLN) |
| C.21 | Koszty uzyskania przychodow | Total cost basis of sold securities (PLN) |
| C.22 | Dochod / Strata | Net gain or loss (C.20 - C.21) |
| C.23 | Podstawa obliczenia podatku | Tax base (= C.22 if positive, otherwise 0) |
| C.24 | Podatek 19% | Capital gains tax (C.23 x 19%) |
| D.25 | Przychody z dywidend | Total dividends received (PLN) |
| D.26 | Zryczaltowany podatek 19% | Flat tax on dividends (D.25 x 19%) |
| E.27 | Podatek zaplacony za granica | Foreign tax credit (withholding in PLN) |
| E.28 | Podatek do zaplaty | Dividend tax remaining after credit (D.26 - E.27) |

## App pages

- **Import** — drag & drop CSV upload with progress indicator
- **Dashboard** — summary cards (capital gains, dividends, foreign tax paid, PL tax owed), monthly bar chart, tax breakdown pie chart
- **Transactions** — full table of all trades with PLN conversion, filterable by symbol and type, sortable, paginated
- **Dividends** — table of all dividend payments with foreign withholding and net tax owed per row
- **PIT-38 Guide** — pre-filled field values, step-by-step filing instructions, printable via browser

## Project structure

```
pitly/
├── src/
│   ├── Pitly.Core/                # Class library — no external dependencies
│   │   ├── Models/                # Trade, Dividend, TaxSummary, Pit38Fields, TradeResult
│   │   ├── Parsing/               # IbActivityParser — CSV parser for IB format
│   │   ├── Services/              # NbpExchangeRateService — NBP API client with cache
│   │   └── Tax/                   # CapitalGainsTaxEngine (FIFO), DividendTaxEngine, TaxCalculator
│   └── Pitly.Api/                 # .NET 10 Minimal API
│       ├── Data/                  # EF Core DbContext + entities, SQLite
│       └── Program.cs             # API endpoints
├── frontend/                      # React 18 + TypeScript + Vite + Tailwind CSS v4
│   └── src/
│       ├── pages/                 # ImportPage, DashboardPage, TransactionsPage, DividendsPage, Pit38Page
│       ├── components/            # Sidebar
│       ├── api.ts                 # HTTP client
│       ├── format.ts              # PLN/USD/date formatting (Polish conventions)
│       └── types.ts               # TypeScript interfaces
├── samples/                       # Sample IB Activity Statement for testing
├── docker-compose.yml             # Production deployment
└── Pitly.sln                      # .NET solution file
```

## API endpoints

| Method | Path | Description |
|--------|------|-------------|
| POST | `/api/import` | Upload CSV (multipart/form-data), returns sessionId + full results |
| GET | `/api/session/{id}/trades` | Paginated trades (query: page, pageSize, sortBy, sortOrder, symbolFilter) |
| GET | `/api/session/{id}/dividends` | All dividends for session |
| GET | `/api/session/{id}/summary` | Tax summary totals |
| GET | `/api/session/{id}/pit38` | PIT-38 field values |
| GET | `/api/session/{id}/export/csv` | Download all transactions as CSV |

## Docker

```bash
docker-compose up --build
```

Backend on port 7001, frontend on port 3000.

## Sample data

A sample IB Activity Statement is provided at `samples/sample-activity-statement.csv` with fictional trades (AAPL, MSFT, NVDA), dividends, and withholding tax entries for 2024.

## Limitations

- Only processes **Stocks** trades. Forex, options, futures, bonds, and crypto are not supported.
- Only handles **USD** and **EUR** currencies (NBP Table A).
- Loss carryforward from previous years is not calculated — you must apply that manually.
- The app does not submit PIT-38 electronically. You must enter the values into e-Urzad Skarbowy yourself.
- Tax calculations are provided as a tool to assist you. Always verify the results and consult a tax advisor if unsure.

## Tech stack

- **Backend**: .NET 10, C#, Minimal API, Entity Framework Core, SQLite
- **Frontend**: React 18, TypeScript, Vite, Tailwind CSS v4, Recharts, Lucide icons
- **External API**: NBP (Narodowy Bank Polski) exchange rates — no authentication required
