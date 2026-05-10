# VolleySquad Project Status (As of 2026-05-10)

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
