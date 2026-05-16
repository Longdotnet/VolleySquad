// ============================================================
// DashboardPage.tsx
// MVP Business Dashboard — clean, minimal, maintainable.
//
// Layout (top-down):
//   NavBar → CourtStatusBanner → KpiRow → 2-col Grid
//     Left:  MatchPanel, RegisteredPlayers, MatchHistoryTable (paginated)
//     Right: ProfileCard, ActivityFeed (paginated), LeaderboardTable (paginated)
//   Admin section (only visible to Admin):
//     CreateMatchModal, AdminMembersPanel, AdminMatchesPanel
//
// CSS → src/styles/dashboard.css (.dash-* BEM classes)
// ============================================================

import { useCallback, useEffect, useRef, useState } from "react";
import { useNavigate } from "react-router-dom";
import { useAuthStore } from "../store/authStore";
import {
  getMembers,
  getMatches,
  getMembersPaged,
  getMatchesPaged,
  getLeaderboard,
  getMe,
  leaveSlot,
  registerSlot,
  splitTeams,
  createMatch,
  updateMatch,
  deleteMatch,
  updateMember,
  deleteMember,
  addMember,
  type PagedResult,
} from "../api/matchApi";
import { getPublicActivities, type PublicActivity } from "../api/publicApi";
import { useMatchHub, type AdminRegisterPayload } from "../hooks/useMatchHub";
import type { Member } from "../types/Member";
import type { Match, Team } from "../types/Match";
import "../styles/dashboard.css";

// ─── Constants ────────────────────────────────────────────────

const FALLBACK_MATCH: Match = {
  id: "00000000-0000-0000-0000-000000000001",
  playDate: new Date(Date.now() + 2 * 24 * 60 * 60 * 1000).toISOString(),
  location: "Chưa có trận nào sắp tới",
  maxSlots: 18,
  registeredMemberIds: [],
  status: "Upcoming",
  isSettled: false,
  feePerPerson: 0,
};

const STATUS_LABEL: Record<string, string> = {
  Upcoming:   "Sắp diễn ra",
  InProgress: "Đang diễn ra",
  Finished:   "Đã kết thúc",
  Settled:    "Đã quyết toán",
  Cancelled:  "Đã hủy",
};

const MATCH_PAGE_SIZE   = 8;
const MEMBER_PAGE_SIZE  = 15;
const FEED_PAGE_SIZE    = 8;
const LB_PAGE_SIZE      = 10;

// ─── Helpers ──────────────────────────────────────────────────

function fmtDate(iso: string): string {
  return new Date(iso).toLocaleDateString("vi-VN", {
    timeZone: "Asia/Ho_Chi_Minh",
    weekday: "short",
    day: "numeric",
    month: "numeric",
  });
}

function fmtDateTime(iso: string): string {
  return new Date(iso).toLocaleString("vi-VN", {
    timeZone: "Asia/Ho_Chi_Minh",
    day: "2-digit",
    month: "2-digit",
    year: "numeric",
    hour: "2-digit",
    minute: "2-digit",
  });
}

function fmtDateTimeInput(iso: string): string {
  const d = new Date(iso);
  const pad = (n: number) => String(n).padStart(2, "0");
  return `${d.getFullYear()}-${pad(d.getMonth() + 1)}-${pad(d.getDate())}T${pad(d.getHours())}:${pad(d.getMinutes())}`;
}

function timeUntil(iso: string): string {
  const diff = new Date(iso).getTime() - Date.now();
  if (diff <= 0) return "Đã qua";
  const hrs = Math.floor(diff / 3_600_000);
  if (hrs < 1) return `${Math.ceil(diff / 60000)} phút nữa`;
  if (hrs < 24) return `${hrs} giờ nữa`;
  return `${Math.floor(hrs / 24)} ngày nữa`;
}

function timeAgo(iso: string): string {
  const diff = Date.now() - new Date(iso).getTime();
  const sec = Math.round(diff / 1000);
  if (sec < 60) return "vừa xong";
  const min = Math.round(sec / 60);
  if (min < 60) return `${min} phút trước`;
  const hrs = Math.round(min / 60);
  if (hrs < 24) return `${hrs} giờ trước`;
  return `${Math.round(hrs / 24)} ngày trước`;
}

function fmtFee(fee: number): string {
  if (fee <= 0) return "—";
  return fee.toLocaleString("vi-VN") + "đ";
}

function avatarUrl(name: string): string {
  return `https://ui-avatars.com/api/?name=${encodeURIComponent(name)}&background=random&color=fff&bold=true&size=64`;
}

function getTier(sp: number): { label: string; cls: string; icon: string } {
  if (sp >= 85) return { label: "Diamond",  cls: "dash-profile__tier--diamond",  icon: "💎" };
  if (sp >= 70) return { label: "Platinum", cls: "dash-profile__tier--platinum", icon: "🏆" };
  if (sp >= 55) return { label: "Gold",     cls: "dash-profile__tier--gold",     icon: "🥇" };
  if (sp >= 40) return { label: "Silver",   cls: "dash-profile__tier--silver",   icon: "🥈" };
  return              { label: "Bronze",   cls: "dash-profile__tier--bronze",   icon: "🥉" };
}

function statusPillCls(status: string): string {
  const map: Record<string, string> = {
    Upcoming:   "dash-pill--upcoming",
    InProgress: "dash-pill--inprogress",
    Finished:   "dash-pill--finished",
    Settled:    "dash-pill--settled",
    Cancelled:  "dash-pill--cancelled",
  };
  return map[status] ?? "";
}

function feedDotCls(eventType: string): string {
  if (["Join","Registered","Left","Transfer"].some((k) => eventType.includes(k)))
    return "dash-feed__dot--join";
  if (eventType.includes("Match")) return "dash-feed__dot--match";
  if (["Skill","Milestone"].some((k) => eventType.includes(k))) return "dash-feed__dot--skill";
  if (eventType.includes("Rank")) return "dash-feed__dot--rank";
  return "";
}

function feedIcon(eventType: string): string {
  const map: Record<string, string> = {
    MemberRegistered:  "👤", MemberJoinedMatch: "✅", MemberLeftMatch:   "🚪",
    SlotTransferred:   "🔄", SlotAccepted:      "🤝", MatchCreated:      "🏐",
    MatchStarted:      "▶️", MatchFinished:     "🏁", MatchSettled:      "💰",
    MatchCancelled:    "❌", MatchFullyBooked:  "🔒", SkillPointUpdated: "⚡",
    RankingUpdated:    "📊", MilestoneReached:  "🎯",
  };
  return map[eventType] ?? "📌";
}

// ─── Pagination Component ─────────────────────────────────────

interface PaginationProps {
  page: number;
  totalPages: number;
  totalCount: number;
  pageSize: number;
  loading?: boolean;
  onPage: (p: number) => void;
}

function Pagination({ page, totalPages, totalCount, pageSize, loading, onPage }: PaginationProps) {
  if (totalPages <= 1) return null;

  const from = (page - 1) * pageSize + 1;
  const to   = Math.min(page * pageSize, totalCount);

  const pages: number[] = [];
  const delta = 1;
  for (let i = Math.max(1, page - delta); i <= Math.min(totalPages, page + delta); i++) {
    pages.push(i);
  }

  return (
    <div
      style={{
        display: "flex",
        alignItems: "center",
        justifyContent: "space-between",
        padding: "0.75rem 1.25rem",
        borderTop: "1px solid rgba(255,255,255,0.05)",
        flexWrap: "wrap",
        gap: "0.5rem",
      }}
    >
      <span style={{ fontSize: 12, color: "#8b949e" }}>
        {from}–{to} / {totalCount}
      </span>
      <div style={{ display: "flex", gap: 4 }}>
        <button
          className="dash-btn dash-btn--sm dash-btn--secondary"
          onClick={() => onPage(page - 1)}
          disabled={page <= 1 || loading}
          aria-label="Trang trước"
        >
          ‹
        </button>
        {pages[0] > 1 && (
          <>
            <button className="dash-btn dash-btn--sm dash-btn--secondary" onClick={() => onPage(1)} disabled={loading}>1</button>
            {pages[0] > 2 && <span style={{ color: "#8b949e", alignSelf: "center", padding: "0 4px" }}>…</span>}
          </>
        )}
        {pages.map((p) => (
          <button
            key={p}
            className={`dash-btn dash-btn--sm ${p === page ? "dash-btn--primary" : "dash-btn--secondary"}`}
            onClick={() => onPage(p)}
            disabled={loading || p === page}
          >
            {p}
          </button>
        ))}
        {pages[pages.length - 1] < totalPages && (
          <>
            {pages[pages.length - 1] < totalPages - 1 && <span style={{ color: "#8b949e", alignSelf: "center", padding: "0 4px" }}>…</span>}
            <button className="dash-btn dash-btn--sm dash-btn--secondary" onClick={() => onPage(totalPages)} disabled={loading}>{totalPages}</button>
          </>
        )}
        <button
          className="dash-btn dash-btn--sm dash-btn--secondary"
          onClick={() => onPage(page + 1)}
          disabled={page >= totalPages || loading}
          aria-label="Trang tiếp"
        >
          ›
        </button>
      </div>
    </div>
  );
}

// ─── Modal Wrapper ────────────────────────────────────────────

interface ModalProps {
  title: string;
  onClose: () => void;
  children: React.ReactNode;
  width?: number;
}

function Modal({ title, onClose, children, width = 480 }: ModalProps) {
  const overlayRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    const onKey = (e: KeyboardEvent) => { if (e.key === "Escape") onClose(); };
    document.addEventListener("keydown", onKey);
    return () => document.removeEventListener("keydown", onKey);
  }, [onClose]);

  return (
    <div
      ref={overlayRef}
      onClick={(e) => { if (e.target === overlayRef.current) onClose(); }}
      style={{
        position: "fixed", inset: 0, zIndex: 999,
        background: "rgba(0,0,0,0.6)",
        backdropFilter: "blur(4px)",
        display: "flex", alignItems: "center", justifyContent: "center",
        padding: "1rem",
      }}
    >
      <div
        style={{
          background: "#131920",
          border: "1px solid rgba(255,255,255,0.1)",
          borderRadius: 16,
          width: "100%",
          maxWidth: width,
          maxHeight: "90vh",
          overflow: "auto",
        }}
      >
        <div style={{
          display: "flex", alignItems: "center", justifyContent: "space-between",
          padding: "1rem 1.25rem",
          borderBottom: "1px solid rgba(255,255,255,0.07)",
        }}>
          <div style={{ fontSize: 15, fontWeight: 700, color: "#e6edf3" }}>{title}</div>
          <button
            className="dash-nav__btn"
            onClick={onClose}
            aria-label="Đóng"
          >✕</button>
        </div>
        <div style={{ padding: "1.25rem" }}>{children}</div>
      </div>
    </div>
  );
}

// ─── Form Field helper ────────────────────────────────────────

function Field({
  label, children,
}: { label: string; children: React.ReactNode }) {
  return (
    <div style={{ marginBottom: "0.875rem" }}>
      <label style={{ display: "block", fontSize: 12, color: "#8b949e", marginBottom: 5, fontWeight: 600 }}>
        {label}
      </label>
      {children}
    </div>
  );
}

const inputStyle: React.CSSProperties = {
  width: "100%",
  background: "rgba(255,255,255,0.04)",
  border: "1px solid rgba(255,255,255,0.1)",
  borderRadius: 8,
  padding: "0.5rem 0.75rem",
  color: "#e6edf3",
  fontSize: 13.5,
  outline: "none",
  boxSizing: "border-box",
};

// ─── CreateMatchModal ─────────────────────────────────────────

interface CreateMatchModalProps {
  onClose: () => void;
  onCreated: (m: Match) => void;
}

function CreateMatchModal({ onClose, onCreated }: CreateMatchModalProps) {
  const [playDate, setPlayDate]   = useState("");
  const [location, setLocation]   = useState("");
  const [maxSlots, setMaxSlots]   = useState(18);
  const [saving,   setSaving]     = useState(false);
  const [err,      setErr]        = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!playDate || !location.trim()) {
      setErr("Vui lòng điền đầy đủ thông tin.");
      return;
    }
    setSaving(true);
    setErr("");
    try {
      const m = await createMatch({ playDate: new Date(playDate).toISOString(), location: location.trim(), maxSlots });
      onCreated(m);
      onClose();
    } catch (ex: unknown) {
      const msg = (ex as { response?: { data?: string } })?.response?.data;
      setErr(msg ?? "Tạo trận thất bại.");
    } finally {
      setSaving(false);
    }
  };

  return (
    <Modal title="Tạo trận đấu mới" onClose={onClose}>
      <form onSubmit={handleSubmit}>
        {err && (
          <div className="dash-alert dash-alert--error" style={{ marginBottom: "0.875rem" }}>
            ⚠️ {err}
          </div>
        )}
        <Field label="Ngày giờ thi đấu">
          <input type="datetime-local" style={inputStyle} value={playDate} onChange={(e) => setPlayDate(e.target.value)} required />
        </Field>
        <Field label="Địa điểm">
          <input type="text" style={inputStyle} value={location} onChange={(e) => setLocation(e.target.value)} placeholder="VD: Sân bóng chuyền Quận 1" required />
        </Field>
        <Field label="Số slot tối đa">
          <input type="number" style={inputStyle} value={maxSlots} min={6} max={30} onChange={(e) => setMaxSlots(Number(e.target.value))} />
        </Field>
        <div style={{ display: "flex", gap: "0.625rem", justifyContent: "flex-end", marginTop: "1rem" }}>
          <button type="button" className="dash-btn dash-btn--secondary" onClick={onClose}>Huỷ</button>
          <button type="submit" className="dash-btn dash-btn--primary" disabled={saving}>
            {saving ? <><span className="dash-spinner" /> Đang tạo...</> : "Tạo trận"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

// ─── EditMatchModal ───────────────────────────────────────────

interface EditMatchModalProps {
  match: Match;
  onClose: () => void;
  onUpdated: (m: Match) => void;
}

function EditMatchModal({ match, onClose, onUpdated }: EditMatchModalProps) {
  const [playDate,    setPlayDate]    = useState(fmtDateTimeInput(match.playDate));
  const [location,    setLocation]    = useState(match.location);
  const [maxSlots,    setMaxSlots]    = useState(match.maxSlots);
  const [feePerPerson, setFeePerPerson] = useState(match.feePerPerson);
  const [saving,      setSaving]      = useState(false);
  const [err,         setErr]         = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!playDate || !location.trim()) { setErr("Vui lòng điền đầy đủ thông tin."); return; }
    setSaving(true); setErr("");
    try {
      const m = await updateMatch(match.id, { playDate: new Date(playDate).toISOString(), location: location.trim(), maxSlots, feePerPerson });
      onUpdated(m);
      onClose();
    } catch (ex: unknown) {
      const msg = (ex as { response?: { data?: string } })?.response?.data;
      setErr(msg ?? "Cập nhật thất bại.");
    } finally { setSaving(false); }
  };

  return (
    <Modal title="Chỉnh sửa trận đấu" onClose={onClose}>
      <form onSubmit={handleSubmit}>
        {err && <div className="dash-alert dash-alert--error" style={{ marginBottom: "0.875rem" }}>⚠️ {err}</div>}
        <Field label="Ngày giờ thi đấu">
          <input type="datetime-local" style={inputStyle} value={playDate} onChange={(e) => setPlayDate(e.target.value)} required />
        </Field>
        <Field label="Địa điểm">
          <input type="text" style={inputStyle} value={location} onChange={(e) => setLocation(e.target.value)} required />
        </Field>
        <Field label="Số slot tối đa">
          <input type="number" style={inputStyle} value={maxSlots} min={6} max={30} onChange={(e) => setMaxSlots(Number(e.target.value))} />
        </Field>
        <Field label="Phí sân / người (VNĐ)">
          <input type="number" style={inputStyle} value={feePerPerson} min={0} step={1000} onChange={(e) => setFeePerPerson(Number(e.target.value))} placeholder="0 = chưa chốt" />
        </Field>
        <div style={{ display: "flex", gap: "0.625rem", justifyContent: "flex-end", marginTop: "1rem" }}>
          <button type="button" className="dash-btn dash-btn--secondary" onClick={onClose}>Huỷ</button>
          <button type="submit" className="dash-btn dash-btn--primary" disabled={saving}>
            {saving ? <><span className="dash-spinner" /> Đang lưu...</> : "Lưu thay đổi"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

// ─── EditMemberModal ──────────────────────────────────────────

interface EditMemberModalProps {
  member: Member;
  onClose: () => void;
  onUpdated: (m: Member) => void;
}

function EditMemberModal({ member, onClose, onUpdated }: EditMemberModalProps) {
  const [name,       setName]       = useState(member.name);
  const [skillPoint, setSkillPoint] = useState(member.skillPoint);
  const [balance,    setBalance]    = useState(member.balance);
  const [saving,     setSaving]     = useState(false);
  const [err,        setErr]        = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setSaving(true); setErr("");
    try {
      const updated = await updateMember(member.id, {
        name: name.trim() !== member.name ? name.trim() : undefined,
        skillPoint,
        balance,
      });
      onUpdated(updated);
      onClose();
    } catch (ex: unknown) {
      const msg = (ex as { response?: { data?: string } })?.response?.data;
      setErr(msg ?? "Cập nhật thất bại.");
    } finally { setSaving(false); }
  };

  return (
    <Modal title={`Chỉnh sửa: ${member.name}`} onClose={onClose}>
      <form onSubmit={handleSubmit}>
        {err && <div className="dash-alert dash-alert--error" style={{ marginBottom: "0.875rem" }}>⚠️ {err}</div>}
        <Field label="Tên hiển thị">
          <input type="text" style={inputStyle} value={name} onChange={(e) => setName(e.target.value)} required />
        </Field>
        <Field label="Skill Point (0–100)">
          <input type="number" style={inputStyle} value={skillPoint} min={0} max={100} onChange={(e) => setSkillPoint(Number(e.target.value))} />
        </Field>
        <Field label="Số dư (VNĐ)">
          <input type="number" style={inputStyle} value={Number(balance)} min={0} step={1000} onChange={(e) => setBalance(Number(e.target.value) as unknown as typeof balance)} />
        </Field>
        <div style={{ display: "flex", gap: "0.625rem", justifyContent: "flex-end", marginTop: "1rem" }}>
          <button type="button" className="dash-btn dash-btn--secondary" onClick={onClose}>Huỷ</button>
          <button type="submit" className="dash-btn dash-btn--primary" disabled={saving}>
            {saving ? <><span className="dash-spinner" /> Đang lưu...</> : "Lưu thay đổi"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

// ─── AddMemberModal ───────────────────────────────────────────

interface AddMemberModalProps {
  onClose: () => void;
  onCreated: () => void;
}

function AddMemberModal({ onClose, onCreated }: AddMemberModalProps) {
  const [name,       setName]       = useState("");
  const [skillPoint, setSkillPoint] = useState(50);
  const [balance,    setBalance]    = useState(0);
  const [password,   setPassword]   = useState("");
  const [role,       setRole]       = useState("Member");
  const [saving,     setSaving]     = useState(false);
  const [err,        setErr]        = useState("");

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    if (!name.trim() || !password) { setErr("Điền đầy đủ tên và mật khẩu."); return; }
    setSaving(true); setErr("");
    try {
      await addMember({ name: name.trim(), skillPoint, balance, role, password });
      onCreated();
      onClose();
    } catch (ex: unknown) {
      const msg = (ex as { response?: { data?: string } })?.response?.data;
      setErr(msg ?? "Thêm thành viên thất bại.");
    } finally { setSaving(false); }
  };

  return (
    <Modal title="Thêm thành viên mới" onClose={onClose}>
      <form onSubmit={handleSubmit}>
        {err && <div className="dash-alert dash-alert--error" style={{ marginBottom: "0.875rem" }}>⚠️ {err}</div>}
        <Field label="Tên đăng nhập"><input type="text" style={inputStyle} value={name} onChange={(e) => setName(e.target.value)} required /></Field>
        <Field label="Mật khẩu"><input type="password" style={inputStyle} value={password} onChange={(e) => setPassword(e.target.value)} required /></Field>
        <Field label="Skill Point"><input type="number" style={inputStyle} value={skillPoint} min={0} max={100} onChange={(e) => setSkillPoint(Number(e.target.value))} /></Field>
        <Field label="Số dư (VNĐ)"><input type="number" style={inputStyle} value={balance} min={0} step={1000} onChange={(e) => setBalance(Number(e.target.value))} /></Field>
        <Field label="Vai trò">
          <select style={inputStyle} value={role} onChange={(e) => setRole(e.target.value)}>
            <option value="Member">Member</option>
            <option value="Admin">Admin</option>
          </select>
        </Field>
        <div style={{ display: "flex", gap: "0.625rem", justifyContent: "flex-end", marginTop: "1rem" }}>
          <button type="button" className="dash-btn dash-btn--secondary" onClick={onClose}>Huỷ</button>
          <button type="submit" className="dash-btn dash-btn--primary" disabled={saving}>
            {saving ? <><span className="dash-spinner" /> Đang thêm...</> : "Thêm thành viên"}
          </button>
        </div>
      </form>
    </Modal>
  );
}

// ─── AdminMembersPanel ────────────────────────────────────────

interface AdminMembersPanelProps {
  currentUserId: string;
  onError: (msg: string) => void;
  onSuccess: (msg: string) => void;
}

function AdminMembersPanel({ currentUserId, onError, onSuccess }: AdminMembersPanelProps) {
  const [result,   setResult]   = useState<PagedResult<Member> | null>(null);
  const [page,     setPage]     = useState(1);
  const [search,   setSearch]   = useState("");
  const [loading,  setLoading]  = useState(true);
  const [editing,  setEditing]  = useState<Member | null>(null);
  const [adding,   setAdding]   = useState(false);
  const [deleting, setDeleting] = useState<string | null>(null);
  const searchRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const load = useCallback((p: number, q: string) => {
    setLoading(true);
    getMembersPaged(p, MEMBER_PAGE_SIZE, q)
      .then((r) => { setResult(r); setPage(p); })
      .catch(() => onError("Không thể tải danh sách thành viên."))
      .finally(() => setLoading(false));
  }, [onError]);

  useEffect(() => { load(1, search); }, [load]); // eslint-disable-line

  const handleSearch = (q: string) => {
    setSearch(q);
    if (searchRef.current) clearTimeout(searchRef.current);
    searchRef.current = setTimeout(() => load(1, q), 400);
  };

  const handleDelete = async (id: string, name: string) => {
    if (!confirm(`Xoá thành viên "${name}"? Không thể hoàn tác.`)) return;
    setDeleting(id);
    try {
      await deleteMember(id);
      onSuccess(`Đã xoá ${name}`);
      load(page, search);
    } catch (ex: unknown) {
      const msg = (ex as { response?: { data?: string } })?.response?.data;
      onError(msg ?? "Xoá thất bại.");
    } finally { setDeleting(null); }
  };

  return (
    <>
      {editing && (
        <EditMemberModal
          member={editing}
          onClose={() => setEditing(null)}
          onUpdated={(m) => {
            onSuccess(`Đã cập nhật ${m.name}`);
            setEditing(null);
            load(page, search);
          }}
        />
      )}
      {adding && (
        <AddMemberModal
          onClose={() => setAdding(false)}
          onCreated={() => { onSuccess("Đã thêm thành viên mới"); load(1, search); }}
        />
      )}

      <div className="dash-card" style={{ marginTop: "1rem" }}>
        <div className="dash-card__header">
          <div className="dash-card__title">
            <span className="dash-card__title-icon">👥</span>
            Quản lý thành viên
          </div>
          <button className="dash-btn dash-btn--sm dash-btn--success" onClick={() => setAdding(true)}>
            + Thêm
          </button>
        </div>

        <div style={{ padding: "0.75rem 1.25rem", borderBottom: "1px solid rgba(255,255,255,0.05)" }}>
          <input
            type="search"
            placeholder="Tìm theo tên..."
            style={{ ...inputStyle, width: 240 }}
            value={search}
            onChange={(e) => handleSearch(e.target.value)}
          />
        </div>

        {loading ? (
          <div className="dash-card__body">
            {[0,1,2].map((i) => <span key={i} className="dash-skel" style={{ height: 36, display: "block", marginBottom: 8 }} />)}
          </div>
        ) : !result || result.items.length === 0 ? (
          <div className="dash-empty"><div className="dash-empty__text">Không có kết quả</div></div>
        ) : (
          <>
            <div className="dash-history__scroll">
              <table className="dash-history__table">
                <thead>
                  <tr>
                    <th>Tên</th>
                    <th>Vai trò</th>
                    <th>SP</th>
                    <th>Số dư</th>
                    <th>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {result.items.map((m) => (
                    <tr key={m.id}>
                      <td style={{ display: "flex", alignItems: "center", gap: 8 }}>
                        <img src={avatarUrl(m.name)} alt="" style={{ width: 24, height: 24, borderRadius: "50%" }} aria-hidden="true" />
                        {m.name}
                        {m.id === currentUserId && (
                          <span style={{ fontSize: 10, color: "#3b82f6", marginLeft: 2 }}>bạn</span>
                        )}
                      </td>
                      <td>
                        <span className={`dash-pill ${m.role === "Admin" ? "dash-pill--upcoming" : "dash-pill--finished"}`}>
                          {m.role}
                        </span>
                      </td>
                      <td style={{ fontWeight: 700, color: "#e6edf3" }}>{m.skillPoint}</td>
                      <td style={{ color: m.balance > 0 ? "#22c55e" : "#8b949e" }}>
                        {Number(m.balance) > 0 ? `${Math.floor(Number(m.balance) / 1000)}k` : "—"}
                      </td>
                      <td>
                        <div style={{ display: "flex", gap: 6 }}>
                          <button
                            className="dash-btn dash-btn--sm dash-btn--secondary"
                            onClick={() => setEditing(m)}
                          >
                            ✏️
                          </button>
                          <button
                            className="dash-btn dash-btn--sm dash-btn--secondary"
                            onClick={() => handleDelete(m.id, m.name)}
                            disabled={deleting === m.id || m.id === currentUserId}
                            style={{ color: "#ef4444" }}
                          >
                            {deleting === m.id ? <span className="dash-spinner" /> : "🗑️"}
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>

            <Pagination
              page={page}
              totalPages={result.totalPages}
              totalCount={result.totalCount}
              pageSize={MEMBER_PAGE_SIZE}
              loading={loading}
              onPage={(p) => load(p, search)}
            />
          </>
        )}
      </div>
    </>
  );
}

// ─── AdminMatchesPanel ────────────────────────────────────────

interface AdminMatchesPanelProps {
  onError: (msg: string) => void;
  onSuccess: (msg: string) => void;
}

function AdminMatchesPanel({ onError, onSuccess }: AdminMatchesPanelProps) {
  const [result,    setResult]   = useState<PagedResult<Match> | null>(null);
  const [page,      setPage]     = useState(1);
  const [status,    setStatus]   = useState("");
  const [loading,   setLoading]  = useState(true);
  const [editing,   setEditing]  = useState<Match | null>(null);
  const [creating,  setCreating] = useState(false);
  const [deleting,  setDeleting] = useState<string | null>(null);

  const load = useCallback((p: number, s: string) => {
    setLoading(true);
    getMatchesPaged(p, MATCH_PAGE_SIZE, s)
      .then((r) => { setResult(r); setPage(p); })
      .catch(() => onError("Không thể tải danh sách trận."))
      .finally(() => setLoading(false));
  }, [onError]);

  useEffect(() => { load(1, status); }, [load]); // eslint-disable-line

  const handleDelete = async (id: string) => {
    if (!confirm("Xoá trận này? Không thể hoàn tác.")) return;
    setDeleting(id);
    try {
      await deleteMatch(id);
      onSuccess("Đã xoá trận đấu");
      load(page, status);
    } catch {
      onError("Xoá trận thất bại.");
    } finally { setDeleting(null); }
  };

  return (
    <>
      {creating && (
        <CreateMatchModal
          onClose={() => setCreating(false)}
          onCreated={() => { onSuccess("Đã tạo trận mới!"); load(1, status); }}
        />
      )}
      {editing && (
        <EditMatchModal
          match={editing}
          onClose={() => setEditing(null)}
          onUpdated={() => { onSuccess("Đã cập nhật trận!"); setEditing(null); load(page, status); }}
        />
      )}

      <div className="dash-card">
        <div className="dash-card__header">
          <div className="dash-card__title">
            <span className="dash-card__title-icon">📅</span>
            Quản lý trận đấu
          </div>
          <button className="dash-btn dash-btn--sm dash-btn--success" onClick={() => setCreating(true)}>
            + Tạo trận
          </button>
        </div>

        <div style={{ padding: "0.75rem 1.25rem", borderBottom: "1px solid rgba(255,255,255,0.05)", display: "flex", gap: "0.5rem", flexWrap: "wrap" }}>
          {["", "Upcoming", "InProgress", "Finished", "Settled", "Cancelled"].map((s) => (
            <button
              key={s}
              className={`dash-btn dash-btn--sm ${status === s ? "dash-btn--primary" : "dash-btn--secondary"}`}
              onClick={() => { setStatus(s); load(1, s); }}
            >
              {s || "Tất cả"}
            </button>
          ))}
        </div>

        {loading ? (
          <div className="dash-card__body">
            {[0,1,2].map((i) => <span key={i} className="dash-skel" style={{ height: 36, display: "block", marginBottom: 8 }} />)}
          </div>
        ) : !result || result.items.length === 0 ? (
          <div className="dash-empty"><div className="dash-empty__text">Không có trận nào</div></div>
        ) : (
          <>
            <div className="dash-history__scroll">
              <table className="dash-history__table">
                <thead>
                  <tr>
                    <th>Ngày</th>
                    <th>Địa điểm</th>
                    <th>Người/Slot</th>
                    <th>Trạng thái</th>
                    <th>Thao tác</th>
                  </tr>
                </thead>
                <tbody>
                  {result.items.map((m) => (
                    <tr key={m.id}>
                      <td style={{ whiteSpace: "nowrap" }}>{fmtDate(m.playDate)}</td>
                      <td style={{ maxWidth: 160, overflow: "hidden", textOverflow: "ellipsis" }}>{m.location}</td>
                      <td>{m.registeredMemberIds.length}/{m.maxSlots}</td>
                      <td>
                        <span className={`dash-pill ${statusPillCls(m.status)}`}>
                          {STATUS_LABEL[m.status] ?? m.status}
                        </span>
                      </td>
                      <td>
                        <div style={{ display: "flex", gap: 6 }}>
                          <button className="dash-btn dash-btn--sm dash-btn--secondary" onClick={() => setEditing(m)}>✏️</button>
                          <button
                            className="dash-btn dash-btn--sm dash-btn--secondary"
                            onClick={() => handleDelete(m.id)}
                            disabled={deleting === m.id}
                            style={{ color: "#ef4444" }}
                          >
                            {deleting === m.id ? <span className="dash-spinner" /> : "🗑️"}
                          </button>
                        </div>
                      </td>
                    </tr>
                  ))}
                </tbody>
              </table>
            </div>
            <Pagination
              page={page}
              totalPages={result.totalPages}
              totalCount={result.totalCount}
              pageSize={MATCH_PAGE_SIZE}
              loading={loading}
              onPage={(p) => load(p, status)}
            />
          </>
        )}
      </div>
    </>
  );
}

// ─── CourtStatusBanner ────────────────────────────────────────

type CourtStatus = "open" | "live" | "full" | "none";

interface CourtStatusBannerProps {
  match: Match;
  status: CourtStatus;
  currentUserId: string;
  registering: boolean;
  leaving: boolean;
  isAdmin: boolean;
  onRegister: () => void;
  onLeave: () => void;
  onSplit: () => void;
}

function CourtStatusBanner({
  match, status, currentUserId, registering, leaving, isAdmin, onRegister, onLeave, onSplit,
}: CourtStatusBannerProps) {
  const isRegistered = match.registeredMemberIds.includes(currentUserId);
  const isFallback   = match.id === FALLBACK_MATCH.id;

  const config = {
    open: { icon: "🏐", headline: "Sân đang mở đăng ký",          pill: "Đang mở",  pillDot: true  },
    live: { icon: "🔴", headline: "Trận đang diễn ra",              pill: "Live",     pillDot: true  },
    full: { icon: "🔒", headline: "Sân đã đầy slot",               pill: "Đầy slot", pillDot: false },
    none: { icon: "🌙", headline: "Chưa có trận nào sắp tới",      pill: "Nghỉ",     pillDot: false },
  }[status];

  return (
    <div className={`dash-status-banner dash-status-banner--${status}`}>
      <div className="dash-status-banner__left">
        <div className="dash-status-banner__icon">{config.icon}</div>
        <div className="dash-status-banner__text">
          <div className="dash-status-banner__headline">{config.headline}</div>
          <div className="dash-status-banner__sub">
            <span className="dash-status-banner__pill" style={{ display: "inline-flex", alignItems: "center", gap: 4 }}>
              {config.pillDot && <span className="dash-status-banner__pill-dot" />}
              {config.pill}
            </span>
            {!isFallback && (
              <>
                <span className="dash-status-banner__sub-item">📍 {match.location}</span>
                <span className="dash-status-banner__sub-item">📅 {fmtDateTime(match.playDate)}</span>
                <span className="dash-status-banner__sub-item">👥 {match.registeredMemberIds.length}/{match.maxSlots}</span>
                {match.feePerPerson > 0 && (
                  <span className="dash-status-banner__sub-item">💰 {fmtFee(match.feePerPerson)}/người</span>
                )}
              </>
            )}
          </div>
        </div>
      </div>
      {!isFallback && (
        <div className="dash-status-banner__actions">
          {status === "open" && (
            isRegistered ? (
              <button
                className="dash-btn dash-btn--secondary"
                onClick={onLeave}
                disabled={leaving}
                style={{ color: "#ef4444" }}
              >
                {leaving ? <><span className="dash-spinner" /> Đang rời...</> : "🚪 Rời trận"}
              </button>
            ) : (
              <button
                className="dash-btn dash-btn--primary"
                onClick={onRegister}
                disabled={registering || match.registeredMemberIds.length >= match.maxSlots}
              >
                {registering ? <><span className="dash-spinner" /> Đang đăng ký...</>
                 : match.registeredMemberIds.length >= match.maxSlots ? "🔒 Đã đầy"
                 : "Đăng ký tham gia"}
              </button>
            )
          )}
          {isAdmin && status !== "none" && (
            <button className="dash-btn dash-btn--secondary" onClick={onSplit} disabled={registering}>
              ⚖️ Chia đội
            </button>
          )}
        </div>
      )}
    </div>
  );
}

// ─── KpiRow ───────────────────────────────────────────────────

function KpiRow({ memberCount, nextMatchIn, played, topSp, loading }: {
  memberCount: number; nextMatchIn: string; played: number; topSp: number; loading: boolean;
}) {
  const cards = [
    { icon: "👥", value: String(memberCount), label: "Thành viên",    mod: "blue"   },
    { icon: "⏱",  value: nextMatchIn,          label: "Trận kế tiếp", mod: "green"  },
    { icon: "🏁", value: String(played),        label: "Trận đã chơi", mod: "amber"  },
    { icon: "⚡", value: String(topSp),         label: "SP cao nhất",  mod: "violet" },
  ];
  return (
    <div className="dash-kpi-row">
      {cards.map((c) => (
        <div key={c.label} className={`dash-kpi dash-kpi--${c.mod}`}>
          <div className="dash-kpi__icon">{c.icon}</div>
          <div className="dash-kpi__body">
            <div className="dash-kpi__value">
              {loading ? <span className="dash-skel" style={{ width: 40, height: 24, display: "inline-block" }} /> : c.value}
            </div>
            <div className="dash-kpi__label">{c.label}</div>
          </div>
        </div>
      ))}
    </div>
  );
}

// ─── MatchPanel ───────────────────────────────────────────────

function MatchPanel({ match, loading, currentUserId, registering, leaving, isAdmin, onRegister, onLeave, onEdit, onSplit, teams }: {
  match: Match; loading: boolean; currentUserId: string; registering: boolean; leaving: boolean;
  isAdmin: boolean; onRegister: () => void; onLeave: () => void; onEdit: () => void; onSplit: () => void; teams: Team[];
}) {
  const registered  = match.registeredMemberIds.length;
  const pct         = Math.min(100, (registered / match.maxSlots) * 100);
  const isFull      = registered >= match.maxSlots;
  const isRegistered = match.registeredMemberIds.includes(currentUserId);
  const isFallback  = match.id === FALLBACK_MATCH.id;
  const fillMod     = pct >= 100 ? "--full" : pct >= 75 ? "--warn" : "";

  if (loading) {
    return (
      <div className="dash-card">
        <div className="dash-card__header">
          <div className="dash-card__title"><span className="dash-card__title-icon">🏐</span>Trận kế tiếp</div>
        </div>
        <div className="dash-card__body"><span className="dash-skel dash-skel--card" /></div>
      </div>
    );
  }

  return (
    <div className="dash-card dash-match">
      <div className="dash-card__header">
        <div className="dash-card__title"><span className="dash-card__title-icon">🏐</span>Trận kế tiếp</div>
        <span className={`dash-pill ${statusPillCls(match.status)}`}>{STATUS_LABEL[match.status] ?? match.status}</span>
      </div>
      <div className="dash-card__body">
        {isFallback ? (
          <div className="dash-empty">
            <div className="dash-empty__icon">🌙</div>
            <div className="dash-empty__text">Chưa có trận nào được lên lịch</div>
            <div className="dash-empty__sub">Quay lại sau nhé!</div>
          </div>
        ) : (
          <>
            <div className="dash-match__info-grid">
              <div className="dash-match__info-item">
                <span className="dash-match__info-label">Ngày giờ</span>
                <span className="dash-match__info-value">{fmtDateTime(match.playDate)}</span>
              </div>
              <div className="dash-match__info-item">
                <span className="dash-match__info-label">Còn</span>
                <span className="dash-match__info-value">{timeUntil(match.playDate)}</span>
              </div>
              <div className="dash-match__info-item">
                <span className="dash-match__info-label">Địa điểm</span>
                <span className="dash-match__info-value">{match.location}</span>
              </div>
              <div className="dash-match__info-item">
                <span className="dash-match__info-label">Phí sân</span>
                <span className="dash-match__info-value">{fmtFee(match.feePerPerson)}</span>
              </div>
            </div>
            <div className="dash-match__slots">
              <div className="dash-match__slots-header">
                <span className="dash-match__slots-label">Slot đã đăng ký</span>
                <span className="dash-match__slots-count">{registered} / {match.maxSlots}{isFull ? " · Đầy" : ""}</span>
              </div>
              <div className="dash-match__progress-track">
                <div className={`dash-match__progress-fill dash-match__progress-fill${fillMod}`} style={{ width: `${pct}%` }} />
              </div>
            </div>
            <div className="dash-match__actions">
              {match.status === "Upcoming" && (
                isRegistered ? (
                  <button
                    className="dash-btn dash-btn--secondary"
                    onClick={onLeave}
                    disabled={leaving}
                    style={{ color: "#ef4444" }}
                  >
                    {leaving ? <><span className="dash-spinner" /> Đang rời...</> : "🚪 Rời trận"}
                  </button>
                ) : (
                  <button
                    className="dash-btn dash-btn--primary"
                    onClick={onRegister}
                    disabled={registering || isFull}
                  >
                    {registering ? <><span className="dash-spinner" /> Đang xử lý...</>
                     : isFull      ? "🔒 Đã đầy slot"
                                   : "Đăng ký tham gia"}
                  </button>
                )
              )}
              {isAdmin && (
                <button className="dash-btn dash-btn--secondary" onClick={onSplit} disabled={registering}>
                  ⚖️ Chia đội
                </button>
              )}
              {isAdmin && !isFallback && (
                <button className="dash-btn dash-btn--secondary" onClick={onEdit}>
                  ✏️ Sửa
                </button>
              )}
            </div>
            {teams.length > 0 && (
              <div style={{ marginTop: "1.25rem" }}>
                <div style={{ fontSize: 12, color: "#8b949e", marginBottom: "0.625rem", fontWeight: 600, textTransform: "uppercase", letterSpacing: "0.5px" }}>
                  Kết quả chia đội
                </div>
                <div style={{ display: "flex", gap: "0.75rem", flexWrap: "wrap" }}>
                  {teams.map((team) => (
                    <div key={team.teamName} style={{ flex: "1 1 140px", background: "rgba(255,255,255,0.03)", border: "1px solid rgba(255,255,255,0.07)", borderRadius: 10, padding: "0.75rem" }}>
                      <div style={{ fontSize: 12, fontWeight: 700, color: "#3b82f6", marginBottom: 6 }}>{team.teamName} · {team.totalSkillPoint} pts</div>
                      {team.members.map((m) => (
                        <div key={m.id} style={{ display: "flex", justifyContent: "space-between", fontSize: 12.5, padding: "2px 0", color: "#c9d1d9" }}>
                          <span>{m.name}</span><span style={{ color: "#8b949e" }}>{m.skillPoint}</span>
                        </div>
                      ))}
                    </div>
                  ))}
                </div>
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}

// ─── RegisteredPlayers ────────────────────────────────────────

function RegisteredPlayers({ match, members, currentUserId, loading }: {
  match: Match; members: Member[]; currentUserId: string; loading: boolean;
}) {
  const registeredMembers = members.filter((m) => match.registeredMemberIds.includes(m.id));
  return (
    <div className="dash-card dash-players">
      <div className="dash-card__header">
        <div className="dash-card__title"><span className="dash-card__title-icon">👥</span>Người đã đăng ký</div>
        <span className="dash-card__meta">{match.registeredMemberIds.length} / {match.maxSlots}</span>
      </div>
      {loading ? (
        <div className="dash-players__grid">
          {[0,1,2,3,4].map((i) => <span key={i} className="dash-skel" style={{ width: 90, height: 30, borderRadius: 9999 }} />)}
        </div>
      ) : registeredMembers.length === 0 ? (
        <div className="dash-players__empty">Chưa có ai đăng ký</div>
      ) : (
        <div className="dash-players__grid">
          {registeredMembers.map((m) => (
            <div key={m.id} className={`dash-player-chip${m.id === currentUserId ? " dash-player-chip--me" : ""}`}>
              <img src={avatarUrl(m.name)} alt="" className="dash-player-chip__avatar" aria-hidden="true" />
              {m.name}{m.id === currentUserId ? " (bạn)" : ""}
            </div>
          ))}
        </div>
      )}
    </div>
  );
}

// ─── UpcomingMatchesList (dành cho Member đăng ký) ───────────

interface UpcomingMatchesListProps {
  currentUserId: string;
  registering: boolean;
  onRegister: (matchId: string) => void;
  onLeave: (matchId: string) => void;
  onError: (msg: string) => void;
  refreshKey: number;
}

function UpcomingMatchesList({ currentUserId, registering, onRegister, onLeave, onError, refreshKey }: UpcomingMatchesListProps) {
  const [matches, setMatches] = useState<Match[]>([]);
  const [loading, setLoading] = useState(true);
  const [leavingId, setLeavingId] = useState<string | null>(null);

  const doLeave = async (matchId: string) => {
    setLeavingId(matchId);
    try { await onLeave(matchId); } finally { setLeavingId(null); }
  };

  useEffect(() => {
    setLoading(true);
    getMatchesPaged(1, 10, "Upcoming")
      .then((r) => setMatches(r.items))
      .catch(() => onError("Không tải được danh sách trận sắp tới."))
      .finally(() => setLoading(false));
  }, [refreshKey]); // eslint-disable-line

  return (
    <div className="dash-card">
      <div className="dash-card__header">
        <div className="dash-card__title"><span className="dash-card__title-icon">📅</span>Trận sắp diễn ra</div>
        <span className="dash-card__meta">{matches.length} trận</span>
      </div>
      {loading ? (
        <div className="dash-card__body">
          {[0, 1, 2].map((i) => <span key={i} className="dash-skel" style={{ height: 32, display: "block", marginBottom: 8 }} />)}
        </div>
      ) : matches.length === 0 ? (
        <div className="dash-empty">
          <div className="dash-empty__icon">🌙</div>
          <div className="dash-empty__text">Chưa có trận nào sắp tới</div>
        </div>
      ) : (
        <div className="dash-history__scroll">
          <table className="dash-history__table">
            <thead>
              <tr><th>Ngày</th><th>Địa điểm</th><th>Slot</th><th>Phí</th><th>Đăng ký</th></tr>
            </thead>
            <tbody>
              {matches.map((m) => {
                const isRegistered = m.registeredMemberIds.includes(currentUserId);
                const isFull = m.registeredMemberIds.length >= m.maxSlots;
                return (
                  <tr key={m.id}>
                    <td style={{ whiteSpace: "nowrap" }}>{fmtDate(m.playDate)}<br /><span style={{ fontSize: 11, color: "#8b949e" }}>{timeUntil(m.playDate)}</span></td>
                    <td style={{ maxWidth: 150, overflow: "hidden", textOverflow: "ellipsis" }}>{m.location}</td>
                    <td>{m.registeredMemberIds.length}/{m.maxSlots}</td>
                    <td>{fmtFee(m.feePerPerson)}</td>
                    <td>
                      {isRegistered ? (
                        <button
                          className="dash-btn dash-btn--sm dash-btn--secondary"
                          style={{ color: "#ef4444" }}
                          disabled={leavingId === m.id}
                          onClick={() => doLeave(m.id)}
                        >
                          {leavingId === m.id ? <span className="dash-spinner" /> : "🚪 Rời"}
                        </button>
                      ) : (
                        <button
                          className="dash-btn dash-btn--sm dash-btn--primary"
                          disabled={registering || isFull}
                          onClick={() => onRegister(m.id)}
                        >
                          {isFull ? "🔒 Đầy" : "Đăng ký"}
                        </button>
                      )}
                    </td>
                  </tr>
                );
              })}
            </tbody>
          </table>
        </div>
      )}
    </div>
  );
}

// ─── MatchHistoryTable (server-paginated) ─────────────────────

function MatchHistoryTable({ onError }: { onError: (msg: string) => void }) {
  const [result,  setResult]  = useState<PagedResult<Match> | null>(null);
  const [page,    setPage]    = useState(1);
  const [status,  setStatus]  = useState("");
  const [loading, setLoading] = useState(true);

  const load = useCallback((p: number, s: string) => {
    setLoading(true);
    getMatchesPaged(p, MATCH_PAGE_SIZE, s)
      .then((r) => { setResult(r); setPage(p); })
      .catch(() => onError("Không tải được lịch sử trận."))
      .finally(() => setLoading(false));
  }, [onError]);

  useEffect(() => { load(1, status); }, [load]); // eslint-disable-line

  const sorted = result?.items ?? [];

  return (
    <div className="dash-card dash-history">
      <div className="dash-card__header">
        <div className="dash-card__title"><span className="dash-card__title-icon">📋</span>Lịch sử trận đấu</div>
        <span className="dash-card__meta">{result?.totalCount ?? 0} trận</span>
      </div>

      <div style={{ padding: "0.5rem 1.25rem", borderBottom: "1px solid rgba(255,255,255,0.05)", display: "flex", gap: 6, flexWrap: "wrap" }}>
        {["", "Upcoming", "Finished", "Settled"].map((s) => (
          <button
            key={s}
            className={`dash-btn dash-btn--sm ${status === s ? "dash-btn--primary" : "dash-btn--secondary"}`}
            onClick={() => { setStatus(s); load(1, s); }}
          >
            {s ? STATUS_LABEL[s] : "Tất cả"}
          </button>
        ))}
      </div>

      {loading ? (
        <div className="dash-card__body">
          {[0,1,2,3].map((i) => <span key={i} className="dash-skel" style={{ height: 20, display: "block", marginBottom: 12 }} />)}
        </div>
      ) : sorted.length === 0 ? (
        <div className="dash-empty"><div className="dash-empty__icon">📋</div><div className="dash-empty__text">Chưa có trận nào</div></div>
      ) : (
        <>
          <div className="dash-history__scroll">
            <table className="dash-history__table">
              <thead>
                <tr><th>Ngày</th><th>Địa điểm</th><th>Người</th><th>Trạng thái</th><th>Phí</th></tr>
              </thead>
              <tbody>
                {sorted.map((m) => (
                  <tr key={m.id}>
                    <td>{fmtDate(m.playDate)}</td>
                    <td style={{ maxWidth: 160, overflow: "hidden", textOverflow: "ellipsis" }}>{m.location}</td>
                    <td>{m.registeredMemberIds.length}/{m.maxSlots}</td>
                    <td><span className={`dash-pill ${statusPillCls(m.status)}`}>{STATUS_LABEL[m.status] ?? m.status}</span></td>
                    <td style={{ color: m.feePerPerson > 0 ? "#c9d1d9" : "#8b949e" }}>{fmtFee(m.feePerPerson)}</td>
                  </tr>
                ))}
              </tbody>
            </table>
          </div>
          {result && (
            <Pagination
              page={page}
              totalPages={result.totalPages}
              totalCount={result.totalCount}
              pageSize={MATCH_PAGE_SIZE}
              loading={loading}
              onPage={(p) => load(p, status)}
            />
          )}
        </>
      )}
    </div>
  );
}

// ─── ProfileCard ──────────────────────────────────────────────

function ProfileCard({ member, matchesJoined }: { member: Member; matchesJoined: number }) {
  const tier  = getTier(member.skillPoint);
  const spPct = Math.min(100, member.skillPoint);
  return (
    <div className="dash-card">
      <div className="dash-profile">
        <div className="dash-profile__avatar-wrap">
          <img src={avatarUrl(member.name)} alt={`Ảnh của ${member.name}`} className="dash-profile__avatar" />
          <span className="dash-profile__online" aria-label="Đang online" />
        </div>
        <div className="dash-profile__name">{member.name}</div>
        <div className="dash-profile__role">{member.role === "Admin" ? "⭐ Quản trị viên" : "Thành viên"}</div>
        <div className="dash-profile__stats">
          <div className="dash-profile__stat">
            <div className="dash-profile__stat-val">{member.skillPoint}</div>
            <div className="dash-profile__stat-lbl">SP</div>
          </div>
          <div className="dash-profile__stat">
            <div className="dash-profile__stat-val">{matchesJoined}</div>
            <div className="dash-profile__stat-lbl">Trận</div>
          </div>
          <div className="dash-profile__stat">
            <div className="dash-profile__stat-val">{Number(member.balance) > 0 ? `${Math.floor(Number(member.balance) / 1000)}k` : "0"}</div>
            <div className="dash-profile__stat-lbl">Số dư</div>
          </div>
        </div>
        <div className="dash-profile__sp-wrap">
          <div style={{ display: "flex", justifyContent: "space-between", fontSize: 11, color: "#8b949e" }}>
            <span>Skill Point</span><span>{member.skillPoint}/100</span>
          </div>
          <div className="dash-profile__sp-track">
            <div className="dash-profile__sp-fill" style={{ width: `${spPct}%` }} />
          </div>
        </div>
        <div className={`dash-profile__tier ${tier.cls}`}><span>{tier.icon}</span>{tier.label}</div>
      </div>
    </div>
  );
}

// ─── ActivityFeed (client-paginated) ─────────────────────────

function ActivityFeed({ activities, loading }: { activities: PublicActivity[]; loading: boolean }) {
  const [page, setPage] = useState(1);
  const totalCount = activities.length;
  const totalPages = Math.ceil(totalCount / FEED_PAGE_SIZE);
  const paged      = activities.slice((page - 1) * FEED_PAGE_SIZE, page * FEED_PAGE_SIZE);

  return (
    <div className="dash-card dash-feed">
      <div className="dash-card__header">
        <div className="dash-card__title"><span className="dash-card__title-icon">📡</span>Hoạt động gần đây</div>
        <span style={{ display: "inline-block", width: 7, height: 7, borderRadius: "50%", background: "#22c55e", boxShadow: "0 0 0 2px rgba(34,197,94,0.2)" }} title="Live" />
      </div>
      <div className="dash-feed__scroll" role="log" aria-live="polite">
        {loading ? (
          Array.from({ length: 5 }).map((_, i) => (
            <div key={i} className="dash-feed__item">
              <span className="dash-skel dash-skel--circle" style={{ width: 28, height: 28, flexShrink: 0 }} />
              <div style={{ flex: 1 }}>
                <span className="dash-skel" style={{ height: 13, display: "block", marginBottom: 5 }} />
                <span className="dash-skel" style={{ height: 11, width: "50%", display: "block" }} />
              </div>
            </div>
          ))
        ) : paged.length === 0 ? (
          <div className="dash-feed__empty">Chưa có hoạt động nào</div>
        ) : (
          paged.map((act, idx) => (
            <div key={idx} className="dash-feed__item">
              <div className={`dash-feed__dot ${feedDotCls(act.eventType)}`}>{feedIcon(act.eventType)}</div>
              <div className="dash-feed__body">
                <div className="dash-feed__msg">{act.message}</div>
                <div className="dash-feed__time">{timeAgo(act.occurredAt)}</div>
              </div>
            </div>
          ))
        )}
      </div>
      <Pagination
        page={page}
        totalPages={totalPages}
        totalCount={totalCount}
        pageSize={FEED_PAGE_SIZE}
        loading={loading}
        onPage={setPage}
      />
    </div>
  );
}

// ─── LeaderboardTable (client-paginated) ─────────────────────

function LeaderboardTable({ members, currentUserId, loading }: {
  members: Member[]; currentUserId: string; loading: boolean;
}) {
  const [page, setPage] = useState(1);
  const sorted     = [...members].sort((a, b) => b.skillPoint - a.skillPoint);
  const totalCount = sorted.length;
  const totalPages = Math.ceil(totalCount / LB_PAGE_SIZE);
  const paged      = sorted.slice((page - 1) * LB_PAGE_SIZE, page * LB_PAGE_SIZE);
  const maxSp      = sorted[0]?.skillPoint ?? 100;

  const rankBase = (page - 1) * LB_PAGE_SIZE;
  const rankNum  = (rank: number) => {
    if (rank === 1) return <span className="dash-rank-num dash-rank-num--1">🥇</span>;
    if (rank === 2) return <span className="dash-rank-num dash-rank-num--2">🥈</span>;
    if (rank === 3) return <span className="dash-rank-num dash-rank-num--3">🥉</span>;
    return <span className="dash-rank-num">{rank}</span>;
  };

  return (
    <div className="dash-card dash-leaderboard">
      <div className="dash-card__header">
        <div className="dash-card__title"><span className="dash-card__title-icon">🏆</span>Bảng xếp hạng</div>
        <span className="dash-card__meta">Top {Math.min(members.length, LB_PAGE_SIZE)}/{members.length}</span>
      </div>
      {loading ? (
        Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="dash-rank-row">
            <span className="dash-skel" style={{ width: 22, height: 14 }} />
            <span className="dash-skel dash-skel--circle" style={{ width: 28, height: 28 }} />
            <span className="dash-skel" style={{ flex: 1, height: 13 }} />
          </div>
        ))
      ) : paged.length === 0 ? (
        <div className="dash-empty"><div className="dash-empty__icon">🏆</div><div className="dash-empty__text">Chưa có dữ liệu</div></div>
      ) : (
        paged.map((m, idx) => {
          const rank   = rankBase + idx + 1;
          const barPct = maxSp > 0 ? (m.skillPoint / maxSp) * 100 : 0;
          const isMe   = m.id === currentUserId;
          return (
            <div key={m.id} className={`dash-rank-row${isMe ? " dash-rank-row--me" : ""}`}>
              {rankNum(rank)}
              <img src={avatarUrl(m.name)} alt="" className="dash-rank-avatar" aria-hidden="true" />
              <span className="dash-rank-name">
                {m.name}
                {isMe && <span style={{ color: "#3b82f6", fontSize: 10, marginLeft: 4 }}>bạn</span>}
              </span>
              <div className="dash-rank-sp-wrap">
                <div className="dash-rank-bar"><div className="dash-rank-bar-fill" style={{ width: `${barPct}%` }} /></div>
                <span className="dash-rank-sp">{m.skillPoint}</span>
              </div>
            </div>
          );
        })
      )}
      <Pagination
        page={page}
        totalPages={totalPages}
        totalCount={totalCount}
        pageSize={LB_PAGE_SIZE}
        loading={loading}
        onPage={setPage}
      />
    </div>
  );
}

// ─── NavBar ───────────────────────────────────────────────────

function NavBar({ userName, userRole, isAdmin, onLogout, onToggleAdmin }: {
  userName: string; userRole: string; isAdmin: boolean;
  onLogout: () => void; onToggleAdmin: () => void;
}) {
  return (
    <nav className="dash-nav" aria-label="Thanh điều hướng chính">
      <a className="dash-nav__brand" href="/" aria-label="VolleySquad trang chủ">
        <div className="dash-nav__logo" aria-hidden="true">🏐</div>
        <span className="dash-nav__app-name">VOLLEY<span>SQUAD</span></span>
      </a>
      <div className="dash-nav__right">
        {isAdmin && (
          <button className="dash-btn dash-btn--sm dash-btn--secondary" onClick={onToggleAdmin} title="Bảng điều khiển Admin">
            ⚙️ Admin
          </button>
        )}
        <div className="dash-nav__user">
          <img src={avatarUrl(userName)} alt={`Ảnh của ${userName}`} className="dash-nav__avatar" />
          <div className="dash-nav__user-info">
            <span className="dash-nav__user-name">{userName}</span>
            <span className="dash-nav__user-role">{userRole === "Admin" ? "Quản trị viên" : "Thành viên"}</span>
          </div>
          <span className={`dash-nav__badge${userRole === "Admin" ? " dash-nav__badge--admin" : ""}`}>
            {userRole === "Admin" ? "Admin" : "Member"}
          </span>
        </div>
        <button className="dash-nav__btn" onClick={onLogout} title="Đăng xuất" aria-label="Đăng xuất">🚪</button>
      </div>
    </nav>
  );
}

// ─── DashboardPage (main) ─────────────────────────────────────

type ActiveTab = "home" | "matches" | "leaderboard" | "profile";

export default function DashboardPage() {
  const [members,           setMembers]           = useState<Member[]>([]);
  const [matches,           setMatches]           = useState<Match[]>([]);
  const [currentMatch,      setCurrentMatch]      = useState<Match>(FALLBACK_MATCH);
  const [activities,        setActivities]        = useState<PublicActivity[]>([]);
  const [teams,             setTeams]             = useState<Team[]>([]);
  const [loadingMembers,    setLoadingMembers]    = useState(true);
  const [loadingMatches,    setLoadingMatches]    = useState(true);
  const [loadingActivities, setLoadingActivities] = useState(true);
  const [registering,       setRegistering]       = useState(false);
  const [splitting,         setSplitting]         = useState(false);
  const [error,             setError]             = useState("");
  const [successMsg,        setSuccessMsg]        = useState("");
  const [activeTab,         setActiveTab]         = useState<ActiveTab>("home");
  const [showAdmin,         setShowAdmin]         = useState(false);
  const [currentMemberData, setCurrentMemberData] = useState<Member | null>(null);
  const [registerKey,       setRegisterKey]       = useState(0);
  const [editingMatch,      setEditingMatch]      = useState(false);
  const [leaving,           setLeaving]           = useState(false);

  const user     = useAuthStore((s) => s.user);
  const logout   = useAuthStore((s) => s.logout);
  const navigate = useNavigate();
  const isAdmin  = user?.role === "Admin";

  // Real-time slot update via SignalR
  const handleSlotUpdated = useCallback(
    (data: { matchId: string; registeredCount: number; maxSlots: number }) => {
      if (data.matchId !== currentMatch.id) return;
      setCurrentMatch((prev) => ({
        ...prev,
        registeredMemberIds: Array(data.registeredCount).fill("") as string[],
        maxSlots: data.maxSlots,
      }));
    },
    [currentMatch.id]
  );
  const handleAdminNotify = useCallback((data: AdminRegisterPayload) => {
    setSuccessMsg(`📣 ${data.memberName} vừa đăng ký! Slot: ${data.registeredCount}/${data.maxSlots}`);
  }, []);

  useMatchHub(currentMatch.id, handleSlotUpdated, handleAdminNotify, isAdmin);

  useEffect(() => {
    if (!successMsg) return;
    const t = setTimeout(() => setSuccessMsg(""), 4000);
    return () => clearTimeout(t);
  }, [successMsg]);

  useEffect(() => {
    if (!error) return;
    const t = setTimeout(() => setError(""), 6000);
    return () => clearTimeout(t);
  }, [error]);

  // Fetch members for leaderboard (use /match/leaderboard — accessible to all roles)
  // Also fetch current user profile via /user/me
  useEffect(() => {
    setLoadingMembers(true);
    const leaderboardFetch = getLeaderboard()
      .then(setMembers)
      .catch(() => {
        // Fallback to admin endpoint if admin
        if (isAdmin) return getMembers().then(setMembers).catch(() => {});
      });
    const meFetch = getMe()
      .then(setCurrentMemberData)
      .catch(() => {});
    Promise.all([leaderboardFetch, meFetch]).finally(() => setLoadingMembers(false));
  }, []); // eslint-disable-line

  const refreshMatches = useCallback(() => {
    setLoadingMatches(true);
    getMatches()
      .then((all) => {
        setMatches(all);
        const inProgress = all.find((m) => m.status === "InProgress");
        const upcoming   = all.find((m) => m.status === "Upcoming");
        if (inProgress) setCurrentMatch(inProgress);
        else if (upcoming) setCurrentMatch(upcoming);
        else setCurrentMatch(FALLBACK_MATCH);
      })
      .catch(() => {})
      .finally(() => setLoadingMatches(false));
  }, []);

  // Fetch upcoming/inprogress match
  useEffect(() => { refreshMatches(); }, [refreshMatches]);

  // Fetch activity feed
  useEffect(() => {
    setLoadingActivities(true);
    getPublicActivities(50)
      .then(setActivities)
      .catch(() => {})
      .finally(() => setLoadingActivities(false));
  }, []);

  // Derived stats
  const playedCount   = matches.filter((m) => m.status === "Finished" || m.status === "Settled").length;
  const topSp         = members.length > 0 ? Math.max(...members.map((m) => m.skillPoint)) : 0;
  const matchesJoined = user ? matches.filter((m) => m.registeredMemberIds.includes(user.id)).length : 0;

  // Use fetched currentMemberData OR find from members array (leaderboard)
  const currentMember = currentMemberData ?? members.find((m) => m.id === user?.id);

  const courtStatus: CourtStatus = (() => {
    if (currentMatch.id === FALLBACK_MATCH.id) return "none";
    if (currentMatch.status === "InProgress") return "live";
    if (currentMatch.registeredMemberIds.length >= currentMatch.maxSlots) return "full";
    if (currentMatch.status === "Upcoming") return "open";
    return "none";
  })();

  const nextMatchLabel =
    currentMatch.id === FALLBACK_MATCH.id ? "—" : timeUntil(currentMatch.playDate);

  const handleLogout = () => { logout(); navigate("/login"); };

  const handleLeave = async () => {
    setLeaving(true);
    setError("");
    try {
      await leaveSlot(currentMatch.id);
      const all = await getMatches();
      setMatches(all);
      const updated = all.find((m) => m.id === currentMatch.id);
      if (updated) setCurrentMatch(updated);
      setRegisterKey((k) => k + 1);
      setSuccessMsg("Đã rời trận thành công.");
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: string } })?.response?.data;
      setError(msg ?? "Rời trận thất bại.");
    } finally { setLeaving(false); }
  };

  const handleRegister = async (matchId?: string) => {
    const targetId = matchId ?? currentMatch.id;
    setRegistering(true);
    setError("");
    try {
      await registerSlot(targetId);
      const all = await getMatches();
      setMatches(all);
      const updated = all.find((m) => m.id === targetId);
      if (updated && targetId === currentMatch.id) setCurrentMatch(updated);
      else if (updated && currentMatch.id === FALLBACK_MATCH.id) setCurrentMatch(updated);
      // Refresh upcoming list
      setRegisterKey((k) => k + 1);
      // Also refresh current member data to get latest info
      getMe().then(setCurrentMemberData).catch(() => {});
      setSuccessMsg("Đăng ký thành công! 🎉");
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: string } })?.response?.data;
      setError(msg ?? "Đăng ký thất bại. Vui lòng thử lại.");
    } finally { setRegistering(false); }
  };

  const handleSplitTeams = async () => {
    setSplitting(true);
    setError("");
    try {
      const result = await splitTeams();
      setTeams(result);
    } catch {
      setError("Chia đội thất bại. Cần ít nhất vài thành viên đã đăng ký.");
    } finally { setSplitting(false); }
  };

  const bottomNav: { id: ActiveTab; icon: string; label: string }[] = [
    { id: "home",        icon: "🏠", label: "Trang chủ" },
    { id: "matches",     icon: "📅", label: "Trận đấu"  },
    { id: "leaderboard", icon: "🏆", label: "Xếp hạng"  },
    { id: "profile",     icon: "👤", label: "Hồ sơ"     },
  ];

  return (
    <div className="dash-root">
      <NavBar
        userName={user?.name ?? "Thành viên"}
        userRole={user?.role ?? "Member"}
        isAdmin={isAdmin}
        onLogout={handleLogout}
        onToggleAdmin={() => setShowAdmin((v) => !v)}
      />

      <div className="dash-page">
        {/* Alerts */}
        {error && (
          <div className="dash-alert dash-alert--error" role="alert">
            <span aria-hidden="true">⚠️</span> {error}
          </div>
        )}
        {successMsg && (
          <div className="dash-alert dash-alert--success" role="status">
            <span aria-hidden="true">✅</span> {successMsg}
          </div>
        )}

        {/* Admin Panel (collapsible) */}
        {isAdmin && showAdmin && (
          <div style={{ marginBottom: "1.25rem" }}>
            <div style={{
              fontSize: 11, fontWeight: 700, textTransform: "uppercase", letterSpacing: "0.8px",
              color: "#8b949e", marginBottom: "0.75rem", display: "flex", alignItems: "center", gap: 6,
            }}>
              <span style={{ background: "rgba(167,139,250,0.12)", color: "#a78bfa", padding: "2px 8px", borderRadius: 6, fontSize: 10 }}>ADMIN</span>
              Bảng điều khiển quản trị
            </div>
            <AdminMatchesPanel
              onError={setError}
              onSuccess={(msg) => { setSuccessMsg(msg); refreshMatches(); }}
            />
            <AdminMembersPanel
              currentUserId={user?.id ?? ""}
              onError={setError}
              onSuccess={setSuccessMsg}
            />
          </div>
        )}

        {/* Hero: Court Status */}
        <CourtStatusBanner
          match={currentMatch}
          status={courtStatus}
          currentUserId={user?.id ?? ""}
          registering={registering || splitting}
          leaving={leaving}
          isAdmin={isAdmin}
          onRegister={() => handleRegister()}
          onLeave={handleLeave}
          onSplit={handleSplitTeams}
        />

        {/* KPI Row */}
        <KpiRow
          memberCount={members.length}
          nextMatchIn={nextMatchLabel}
          played={playedCount}
          topSp={topSp}
          loading={loadingMembers || loadingMatches}
        />

        {/* Main 2-col Grid */}
        <div className="dash-grid">
          {/* Left column */}
          <div className="dash-col">
            {/* Match edit modal for admin quick-edit */}
            {editingMatch && currentMatch.id !== FALLBACK_MATCH.id && (
              <EditMatchModal
                match={currentMatch}
                onClose={() => setEditingMatch(false)}
                onUpdated={(m) => {
                  setCurrentMatch(m);
                  setMatches((prev) => prev.map((x) => x.id === m.id ? m : x));
                  setSuccessMsg("Cập nhật trận thành công!");
                  setEditingMatch(false);
                }}
              />
            )}

            <MatchPanel
              match={currentMatch}
              loading={loadingMatches}
              currentUserId={user?.id ?? ""}
              registering={registering}
              leaving={leaving}
              isAdmin={isAdmin}
              onRegister={() => handleRegister()}
              onLeave={handleLeave}
              onEdit={() => setEditingMatch(true)}
              onSplit={handleSplitTeams}
              teams={teams}
            />
            <RegisteredPlayers
              match={currentMatch}
              members={members}
              currentUserId={user?.id ?? ""}
              loading={loadingMembers || loadingMatches}
            />
            <UpcomingMatchesList
              currentUserId={user?.id ?? ""}
              registering={registering}
              onRegister={(id) => handleRegister(id)}
              onLeave={async (id) => {
                setLeaving(true);
                setError("");
                try {
                  await leaveSlot(id);
                  const all = await getMatches();
                  setMatches(all);
                  const updated = all.find((m) => m.id === id);
                  if (updated && id === currentMatch.id) setCurrentMatch(updated);
                  setRegisterKey((k) => k + 1);
                  setSuccessMsg("Đã rời trận thành công.");
                } catch (err: unknown) {
                  const msg = (err as { response?: { data?: string } })?.response?.data;
                  setError(msg ?? "Rời trận thất bại.");
                } finally { setLeaving(false); }
              }}
              onError={setError}
              refreshKey={registerKey}
            />
            <MatchHistoryTable onError={setError} />
          </div>

          {/* Right column */}
          <div className="dash-col">
            {currentMember ? (
              <ProfileCard member={currentMember} matchesJoined={matchesJoined} />
            ) : (
              <div className="dash-card">
                <div className="dash-card__body">
                  <span className="dash-skel dash-skel--card" style={{ height: 220 }} />
                </div>
              </div>
            )}
            <ActivityFeed activities={activities} loading={loadingActivities} />
            <LeaderboardTable members={members} currentUserId={user?.id ?? ""} loading={loadingMembers} />
          </div>
        </div>
      </div>

      {/* Mobile bottom nav */}
      <nav className="dash-bottom-nav" aria-label="Điều hướng di động">
        <div className="dash-bottom-nav__inner">
          {bottomNav.map((item) => (
            <button
              key={item.id}
              className={`dash-bottom-nav__item${activeTab === item.id ? " dash-bottom-nav__item--active" : ""}`}
              onClick={() => setActiveTab(item.id)}
              aria-current={activeTab === item.id ? "page" : undefined}
            >
              <span className="dash-bottom-nav__icon">{item.icon}</span>
              {item.label}
            </button>
          ))}
        </div>
      </nav>
    </div>
  );
}
