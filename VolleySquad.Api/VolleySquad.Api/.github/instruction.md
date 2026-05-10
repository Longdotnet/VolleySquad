# Project: VolleySquad - Data-Driven Volleyball Community Platform
**Version:** 1.5.0
**Tech Stack:** .NET 8, EF Core, MassTransit, RabbitMQ, React, TypeScript

## 1. Project Purpose
Nền tảng cộng đồng bóng chuyền, sử dụng kiến trúc Event-Driven để quản lý trận đấu, thành viên, và các tác vụ nền một cách hiệu quả. Hỗ trợ Admin tạo trận, quản lý thành viên, tự động chia đội, và xử lý các nghiệp vụ phức tạp (ranking, payment) một cách bất đồng bộ.

## 2. Technical Architecture
- **Structure:** Event-Driven Architecture with Microservices approach.
  - `VolleySquad.Api`: Main API gateway, responsible for handling user requests and publishing events.
  - `Ranking.Worker`: A background service that consumes `MatchFinishedEvent` to update player skill points.
  - (Future) `Payment.Worker`, `Notification.Worker`.
- **Communication:** Asynchronous messaging via `RabbitMQ` managed by `MassTransit`.
- **Frontend:** Single Page Application (SPA) built with `React` & `TypeScript`.
- **Database:** SQL Server (LocalDB) via Entity Framework Core.
- **Authentication:** JWT Bearer Token (Roles: `Admin`, `Member`).

## 3. Core Entities & Events
- **Member:** `Id (Guid)`, `Name`, `SkillPoint (1-100)`, `Balance (decimal)`, `Role`.
- **Match:** `Id (Guid)`, `PlayDate`, `Location`, `MaxSlots (default 18)`, `RegisteredMemberIds (List<Guid>)`.
- **Event: `MatchFinishedEvent`**: A message contract containing `MatchId` and `WinningTeamId`, published when a match concludes.

## 4. Key Implementation Details
- **Decoupling:** The API is decoupled from heavy business logic. Finishing a match is now a fast operation that publishes an event, instead of a slow, blocking process.
- **Resilience & Scalability:** Background workers can process events independently. If the `Ranking.Worker` is down, events will queue in RabbitMQ and be processed when it's back online, ensuring no data is lost.
- **Team Splitting:** Uses a `Snake Draft` algorithm in `TeamService`.
- **Frontend State:** `Zustand` for global state management, `React Router` for navigation.

## 5. Security Configuration (Program.cs)
- **AuthenticationScheme:** `JwtBearer`.
- **Secret Key:** Stored in `appsettings.json` (should be moved to Secret Manager for production).
- **CORS:** Configured to allow requests from the React frontend (`http://localhost:5173`).

## 6. Current Backlog & Roadmap
- [x] **Architecture:** Refactor from Monolith to Event-Driven with MassTransit & RabbitMQ.
- [x] **Frontend:** Integrate React SPA, implement Login/Dashboard, and connect to backend APIs.
- [ ] **Implement Ranking Logic:** Code the core logic inside `Ranking.Worker` to calculate and update `SkillPoint` based on match results.
- [ ] **Implement Payment Worker:** Create a new worker to handle match fee deductions.
- [ ] **Real-time UI:** Use SignalR to push live updates (e.g., slot counts) to the frontend.
- [ ] **CI/CD:** Set up a GitHub Actions pipeline for automated build and deployment.
- [ ] **AI Features:** Implement advanced team balancing and video analysis.
