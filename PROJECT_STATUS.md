# VolleySquad Project Status (As of 2026-05-15)

---

## MVP DEMO CHECKLIST

Làm theo thứ tự từ trên xuống. Đánh dấu `[x]` khi xong.

### INFRA — Phải chạy trước khi bật app

- [ ] **[INFRA-1]** Chạy SQL Server & apply migration
  ```bash
  # Trong VolleySquad.Api/VolleySquad.Api/
  dotnet ef database update
  ```
- [ ] **[INFRA-2]** Set JWT SecretKey qua User Secrets (phải đủ 32+ ký tự)
  ```bash
  cd VolleySquad.Api/VolleySquad.Api
  dotnet user-secrets set "JwtSettings:SecretKey" "change-me-to-a-random-256bit-key-123456"
  ```
- [ ] **[INFRA-3]** Chạy RabbitMQ qua Docker
  ```bash
  docker run -d --name rabbitmq -p 5672:5672 -p 15672:15672 rabbitmq:3-management
  ```
- [ ] **[INFRA-4]** Seed dữ liệu: tạo 1 Admin + vài Member qua Swagger (`POST /api/match/add-member`), tạo 1 Match (`POST /api/match/create-match`)

### FRONTEND — Fix 2 lỗi critical trước khi demo

- [x] **[FE-1]** `DashboardPage`: Thay `MOCK_MATCH` bằng API call thật
  - Gọi `getMatches()` trong `useEffect`, lấy match đầu tiên có `status === 'Upcoming'`
  - Fallback về `MOCK_MATCH` nếu API không có match nào
  - File: `volleysquad-fe/src/pages/DashboardPage.tsx`

- [x] **[FE-2]** `DashboardPage`: Wire nút "Đăng ký ngay" → `registerSlot(currentMatch.id)`
  - Import `registerSlot` từ `matchApi.ts`
  - Sau khi gọi thành công: fetch lại match để cập nhật slot count (hoặc đợi SignalR push)
  - Xử lý lỗi: hiển thị message (đã đăng ký rồi, hết slot...)
  - File: `volleysquad-fe/src/pages/DashboardPage.tsx`

- [ ] **[FE-3]** Kiểm tra `PlayerCard` hiển thị đúng Balance và SkillPoint từ Zustand store
  - `user` được set từ login response (`LoginResponse.member`) — cần verify backend trả về đúng field

### BACKEND — Verify endpoints chạy đúng

- [ ] **[BE-1]** Test flow qua Swagger: Login → tạo Match → Register Slot → Finish Match → xem Ranking.Worker log
- [ ] **[BE-2]** Verify `Payment.Worker` tiêu thụ `MatchFinalizedEvent` và trừ Balance

### OPTIONAL (nếu còn thời gian)

- [ ] **[OPT-1]** Admin panel nhỏ trên FE để tạo match và bấm Finish/Finalize (hiện chỉ làm được qua Swagger)
- [ ] **[OPT-2]** Hiển thị trạng thái đăng ký của user hiện tại trên MatchBanner (đã đăng ký / chưa)
- [ ] **[OPT-3]** Toast notification thay cho alert/error string

---

## CÁCH CHẠY TOÀN BỘ HỆ THỐNG (Local Dev)

```bash
# Terminal 1 - API
cd VolleySquad.Api/VolleySquad.Api
dotnet run

# Terminal 2 - Ranking Worker
cd Ranking.Worker
dotnet run

# Terminal 3 - Payment Worker
cd Payment.Worker
dotnet run

# Terminal 4 - Frontend
cd volleysquad-fe
npm install
npm run dev
```

---

## I. What has been DONE

### Frontend (`volleysquad-fe` - React + TypeScript)
- **Project Setup:** Initialized a Vite project with React and TypeScript.
- **Styling:** Integrated TailwindCSS and migrated the provided HTML template into reusable React components (`MatchBanner`, `PlayerCard`, `Leaderboard`).
- **Core UI:**
    - Built a fully functional `LoginPage` with form handling and API calls.
    - Built a comprehensive `DashboardPage` that fetches and displays data (members, teams).
- **API Integration:**
    - Configured `axios` for API communication.
    - Implemented API client services (`authApi`, `matchApi`).
    - Fixed all CORS and HTTPS redirection issues between frontend and backend.
- **State Management:** Implemented `zustand` for global state management (JWT token, user info).
- **Routing:** Set up client-side routing using `react-router-dom`, including a `PrivateRoute` guard.

### Backend (`VolleySquad.Api` & `Ranking.Worker` - .NET 8)
- **Architecture Refactoring:** Transitioned the system from a monolith to an **Event-Driven Architecture**.
- **Message Broker Integration:**
    - Installed and configured `MassTransit` with `RabbitMQ` in both the API and a new Worker project.
    - This decouples the system, making it more scalable and resilient.
- **Publisher (`VolleySquad.Api`):**
    - Created a new endpoint `POST /api/match/finish`.
    - When called, this endpoint publishes a `MatchFinishedEvent` to a RabbitMQ message broker. It no longer handles heavy logic directly.
- **Consumer (`Ranking.Worker`):**
    - Created a new, separate `Ranking.Worker` project.
    - This worker runs as a background service.
    - It contains a `MatchFinishedConsumer` that subscribes to the `MatchFinishedEvent`. Currently, it logs the received message.
- **Project Management:** Created a Visual Studio Solution file (`.sln`) to manage both the API and Worker projects together.

## II. What is NOT DONE (Next Steps)

### Immediate Blockers
1.  **Run RabbitMQ:** The application cannot connect to the message broker because it's not running. **Action:** The user needs to install and run RabbitMQ (e.g., via a Docker container).
    ```bash
    docker run -d --hostname my-rabbit --name some-rabbit -p 5672:5672 -p 15672:15672 rabbitmq:3-management
    ```

### Core Logic Implementation
2.  **Implement Ranking Logic in Worker:** The `MatchFinishedConsumer` is the key place for the next feature.
    - **Action:** Add database access to the `Ranking.Worker`.
    - **Action:** Inside the `Consume` method, implement the algorithm to calculate and update `SkillPoint` for players based on the `MatchFinishedEvent` data.
3.  **Implement Payment Logic in a New Worker:**
    - **Action:** Create a `Payment.Worker` similar to `Ranking.Worker`.
    - **Action:** It should listen to the `MatchFinishedEvent` and handle the logic for deducting match fees from members' balances.

### Advanced Features (From "1-Year Pro" Roadmap)
4.  **AI Team Balancer:** Enhance the team splitting algorithm beyond simple `SkillPoint`.
5.  **Video Analysis / Highlight Reel:** Integrate a service for media processing.
6.  **Real-time Frontend:** Use **SignalR** to push real-time updates to the React frontend (e.g., live slot counts, new match notifications).
7.  **CI/CD Pipeline:** Set up a GitHub Actions workflow to automate build, test, and deployment.

---
This summary provides a clear handoff point. The next developer should start by running RabbitMQ, then implementing the ranking logic inside the `Ranking.Worker`.
