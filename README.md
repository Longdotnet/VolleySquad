# 🏐 VolleySquad

A full-stack **volleyball squad management platform** built with an event-driven microservices architecture. Designed to automate team balancing, match lifecycle management, player rankings, and court fee collection for recreational volleyball groups.

---

## ✨ Key Features

| Feature | Description |
|---|---|
| **Smart Team Balancer** | Snake-draft algorithm splits players into balanced teams using `SkillPoint` ratings |
| **Match Lifecycle** | Admins create, manage slots, finish, and finalize matches |
| **Real-time Updates** | SignalR pushes live slot & match status changes to all connected clients |
| **Event-Driven Processing** | RabbitMQ + MassTransit decouple ranking & payment logic from the API |
| **Auto Ranking** | `Ranking.Worker` recalculates player `SkillPoint` after each match |
| **Auto Payment** | `Payment.Worker` deducts court fees proportionally from member balances |
| **JWT Auth & RBAC** | Role-based access control (Admin / Member) with secured endpoints |
| **Rate Limiting** | Fixed-window rate limiter on auth endpoints to prevent brute-force attacks |

---

## 🏛️ System Architecture

```
┌──────────────────────────────────────────────────────────────────────┐
│                          CLIENT (React + TS)                         │
│          Axios REST calls  ◄──────────►  SignalR WebSocket           │
└────────────────────────────┬─────────────────────┬───────────────────┘
                             │ HTTP                │ WS /hub/match
                ┌────────────▼─────────────────────▼──────────┐
                │              VolleySquad.Api                 │
                │   ASP.NET Core  │  JWT Auth  │  Rate Limit   │
                │   EF Core ──► SQL Server                     │
                │   MassTransit ──► Publishes Events           │
                └────────────────────────┬─────────────────────┘
                                         │ RabbitMQ
                          ┌──────────────┴──────────────┐
                          │                             │
              ┌───────────▼──────────┐    ┌────────────▼─────────┐
              │    Ranking.Worker    │    │    Payment.Worker     │
              │  MatchFinishedEvent  │    │  MatchFinalizedEvent  │
              │  → Update SkillPoint │    │  → Deduct Balances    │
              └──────────────────────┘    └───────────────────────┘
```

---

## 🛠️ Tech Stack

**Backend**
- [ASP.NET Core 9](https://dotnet.microsoft.com/) — REST API + SignalR Hub
- [Entity Framework Core](https://learn.microsoft.com/en-us/ef/core/) — ORM with SQL Server
- [MassTransit](https://masstransit.io/) + [RabbitMQ](https://www.rabbitmq.com/) — Async messaging
- JWT Bearer Authentication, PBKDF2 password hashing

**Frontend**
- [React 19](https://react.dev/) + [TypeScript](https://www.typescriptlang.org/) — UI framework
- [Vite](https://vitejs.dev/) — Build tool
- [TailwindCSS v4](https://tailwindcss.com/) — Utility-first styling
- [Zustand](https://zustand-demo.pmnd.rs/) — Global state management
- [@microsoft/signalr](https://www.npmjs.com/package/@microsoft/signalr) — Real-time client

**Infrastructure**
- SQL Server (via Docker or local)
- RabbitMQ (via Docker)

---

## 🗂️ Project Structure

```
VolleySquad/
├── VolleySquad.Api/          # Core REST API + SignalR hub
│   ├── Controllers/          # AuthController, MatchController, UserController
│   ├── Domain/               # Entities, Value Objects, Domain Exceptions
│   ├── Services/             # TeamService (Snake Draft), MemberService
│   ├── Hubs/                 # MatchHub (SignalR real-time)
│   ├── Infrastructure/       # AppDbContext (EF Core)
│   └── Contracts/            # Shared event contracts (DTOs for MassTransit)
│
├── Ranking.Worker/           # Background service: recalculates SkillPoints
├── Payment.Worker/           # Background service: deducts court fees
│
└── volleysquad-fe/           # React + TypeScript frontend
    ├── src/api/              # Axios clients (authApi, matchApi)
    ├── src/pages/            # LoginPage, DashboardPage
    ├── src/components/       # MatchBanner, PlayerCard, Leaderboard
    ├── src/hooks/            # useMatchHub (SignalR)
    └── src/store/            # Zustand auth store
```

---

## 🚀 Getting Started

### Prerequisites
- [.NET 9 SDK](https://dotnet.microsoft.com/download)
- [Node.js 20+](https://nodejs.org/)
- [Docker](https://www.docker.com/) (for SQL Server & RabbitMQ)

### 1. Start Infrastructure

```bash
# SQL Server
docker run -e "ACCEPT_EULA=Y" -e "SA_PASSWORD=YourStrong!Passw0rd" \
  -p 1433:1433 --name sqlserver -d mcr.microsoft.com/mssql/server:2022-latest

# RabbitMQ (with Management UI at http://localhost:15672)
docker run -d --hostname rabbitmq --name rabbitmq \
  -p 5672:5672 -p 15672:15672 rabbitmq:3-management
```

### 2. Run the API

```bash
cd VolleySquad.Api/VolleySquad.Api
dotnet ef database update        # Apply migrations
dotnet run
# → https://localhost:7xxx
```

### 3. Run the Workers

```bash
# In separate terminals:
cd Ranking.Worker && dotnet run
cd Payment.Worker && dotnet run
```

### 4. Run the Frontend

```bash
cd volleysquad-fe
npm install
npm run dev
# → http://localhost:5173
```

---

## 🔑 API Highlights

| Method | Endpoint | Auth | Description |
|---|---|---|---|
| `POST` | `/api/auth/login` | Public | Login, returns JWT |
| `GET` | `/api/match/members` | Admin | List all members |
| `POST` | `/api/match/create` | Admin | Create new match |
| `POST` | `/api/match/register` | Member | Register for a match |
| `POST` | `/api/match/finish` | Admin | Publish `MatchFinishedEvent` → triggers ranking |
| `POST` | `/api/match/finalize` | Admin | Publish `MatchFinalizedEvent` → triggers payment |
| `GET` | `/api/user/leaderboard` | Public | Get ranked leaderboard |

Real-time: **SignalR** hub at `/hub/match` — clients receive instant updates on slot changes and match status.

---

## ⚙️ Design Decisions

- **Event-Driven Architecture** — The API never directly mutates ranking or balance data. It publishes events to RabbitMQ; workers consume and process asynchronously. This keeps the API fast and decoupled.
- **Snake Draft Algorithm** — Sorts players by `SkillPoint` descending, then assigns alternately to two teams (1→A, 2→B, 3→B, 4→A…), ensuring balanced total skill.
- **PBKDF2 Password Hashing** — BCrypt-style key derivation with salt stored alongside hash; raw passwords never persisted.
- **Guid PKs** — Avoids sequential ID enumeration; safe for distributed/multi-DB scenarios.
- **`decimal` for Money** — Avoids IEEE 754 floating-point rounding errors for financial calculations.


---

## 📄 License

MIT
