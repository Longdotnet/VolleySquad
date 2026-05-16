// ============================================================
// PUBLIC API - Gọi các endpoint công khai (không cần auth)
// ============================================================
// Dùng plain fetch() thay vì axiosInstance vì:
//   - axiosInstance inject Authorization header (token)
//   - Endpoint /api/public/* không cần token — không muốn gửi thừa header
//   - Cũng tránh redirect đến login nếu token hết hạn
// ============================================================

const API_BASE = import.meta.env.VITE_API_URL ?? "https://localhost:7202/api";

// ─── Response Types ───────────────────────────────────────────
export interface PublicStats {
  memberCount: number;
  matchesPlayed: number;
  highestSkillPoint: number;
  matchesThisMonth: number;
  upcomingMatches: number;
}

export interface PublicActivity {
  message: string;
  occurredAt: string; // ISO 8601 UTC string — format phía UI
  eventType: string;  // Tên enum VD: "MemberJoinedMatch"
}

// ─── API Calls ────────────────────────────────────────────────

/**
 * Lấy thống kê tổng hợp cho panel trái trang Login.
 * Không cần JWT.
 */
export async function getPublicStats(): Promise<PublicStats> {
  const res = await fetch(`${API_BASE}/public/stats`, {
    headers: { Accept: "application/json" },
    // cache: "no-store" // Bỏ comment nếu muốn luôn lấy fresh data
  });

  if (!res.ok) {
    throw new Error(`[PublicAPI] /public/stats → HTTP ${res.status}`);
  }

  return res.json() as Promise<PublicStats>;
}

/**
 * Lấy danh sách hoạt động gần nhất cho activity ticker.
 * @param count Số lượng hoạt động (mặc định 25, tối đa 50 theo backend)
 */
export async function getPublicActivities(count = 25): Promise<PublicActivity[]> {
  const res = await fetch(`${API_BASE}/public/activities?count=${count}`, {
    headers: { Accept: "application/json" },
  });

  if (!res.ok) {
    throw new Error(`[PublicAPI] /public/activities → HTTP ${res.status}`);
  }

  return res.json() as Promise<PublicActivity[]>;
}
