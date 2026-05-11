// ============================================================
// DASHBOARD PAGE - Màn hình chính với nhiều React pattern quan trọng
// ============================================================
//
// HOOKS DÙNG TRONG FILE NÀY:
// ┌──────────────────────────────────────────────────────────────────────┐
// │ useState<T>     │ Local state cho component                         │
// │ useEffect       │ Side effects: fetch data, subscribe events        │
// │ useCallback     │ Memoize function để tránh re-create mỗi render    │
// │ useNavigate     │ Điều hướng trang (React Router)                   │
// │ useAuthStore    │ Global state (Zustand custom hook)                │
// │ useMatchHub     │ Custom hook — kết nối SignalR                     │
// └──────────────────────────────────────────────────────────────────────┘
//
// ============================================================
// COMPONENT COMPOSITION PATTERN
// ============================================================
// DashboardPage là "container component" (smart component):
//   - Fetch data, quản lý state, xử lý logic
//   - Truyền data xuống "presentational components" qua props
//
// MatchBanner, PlayerCard, Leaderboard là "presentational components" (dumb components):
//   - Chỉ nhận props và render UI
//   - Không biết gì về API, store, hay business logic
//   - Dễ test, dễ tái sử dụng
//
// Tương tự pattern View/ViewModel trong .NET MAUI hoặc MVVM.
import { useCallback, useEffect, useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { useAuthStore } from '../store/authStore';
import { getMembers, splitTeams } from '../api/matchApi';
import type { Member } from '../types/Member';
import type { Match, Team } from '../types/Match';
import MatchBanner from '../components/MatchBanner';
import PlayerCard from '../components/PlayerCard';
import Leaderboard from '../components/Leaderboard';
import { useMatchHub } from '../hooks/useMatchHub';

// ============================================================
// CONSTANT bên ngoài component
// ============================================================
// MOCK_MATCH được định nghĩa ngoài component để:
//   1. Không bị tạo lại mỗi lần component re-render (object mới mỗi render)
//   2. Tránh gây ra infinite loop nếu dùng làm useEffect dependency
// Production: Thay bằng API call GET /api/match/upcoming
const MOCK_MATCH: Match = {
  id: '00000000-0000-0000-0000-000000000001',
  playDate: new Date(Date.now() + 2 * 24 * 60 * 60 * 1000).toISOString(),
  location: 'Giao lưu Sân Bình Minh',
  maxSlots: 18,
  registeredMemberIds: [],
};

export default function DashboardPage() {
  // useState<Member[]>([]) — Generic type + initial value
  // TypeScript tự infer từ [] nhưng không biết đây là Member[] hay never[]
  // → Phải khai báo tường minh <Member[]> để có type safety
  const [members, setMembers] = useState<Member[]>([]);
  const [teams, setTeams] = useState<Team[]>([]);
  const [loadingTeams, setLoadingTeams] = useState(false);
  const [error, setError] = useState('');
  const [currentMatch, setCurrentMatch] = useState<Match>(MOCK_MATCH);

  const user = useAuthStore((s) => s.user);
  const logout = useAuthStore((s) => s.logout);
  const navigate = useNavigate();

  // ============================================================
  // useCallback - Memoize callback để tránh SignalR reconnect thừa
  // ============================================================
  // Vấn đề nếu KHÔNG dùng useCallback:
  //   Mỗi lần DashboardPage re-render → handleSlotUpdated được tạo MỚI
  //   → useMatchHub thấy deps thay đổi (onSlotUpdated mới) → disconnect + reconnect
  //   → Re-render → tạo function mới → reconnect lại... (vòng lặp)
  //
  // useCallback(fn, [deps]): Trả về cùng 1 function reference nếu deps không đổi.
  //   Chỉ re-create function khi currentMatch.id thay đổi (chuyển sang trận khác).
  //
  // Functional update trong setState:
  //   setCurrentMatch((prev) => ({ ...prev, ... }))
  //   Dùng khi giá trị mới phụ thuộc vào giá trị cũ.
  //   Tránh closure stale: nếu dùng setCurrentMatch({ ...currentMatch, ... })
  //   và component re-render nhanh → currentMatch có thể là giá trị cũ.
  const handleSlotUpdated = useCallback((data: { matchId: string; registeredCount: number; maxSlots: number }) => {
    if (data.matchId === currentMatch.id) {
      setCurrentMatch((prev) => ({
        ...prev, // Spread operator: copy tất cả field cũ
        // Tạo mảng giả có độ dài = registeredCount để hiển thị số slot đã đăng ký.
        // Production: API nên trả về mảng memberId thật để render avatar từng người.
        registeredMemberIds: Array(data.registeredCount).fill('') as string[],
        maxSlots: data.maxSlots,
      }));
    }
  }, [currentMatch.id]); // deps: chỉ re-create khi chuyển sang trận khác

  // Custom hook tự động quản lý kết nối SignalR
  useMatchHub(currentMatch.id, handleSlotUpdated);

  // ============================================================
  // useEffect - Fetch data khi component mount
  // ============================================================
  // useEffect(fn, []) — deps rỗng [] = chỉ chạy 1 lần sau lần render đầu tiên.
  //   Tương tự: componentDidMount() trong class component.
  //   Tương tự: OnInitializedAsync() trong Blazor.
  //
  // Tại sao không gọi API trực tiếp trong component body?
  //   Component body chạy mỗi lần render → gọi API vô số lần → server DDoS!
  //   useEffect với [] chỉ gọi 1 lần → đúng behavior mong muốn.
  useEffect(() => {
    getMembers()
      .then(setMembers) // setMembers là function → viết tắt .then((data) => setMembers(data))
      .catch(() => setError('Không thể tải danh sách thành viên. Kiểm tra backend.'));
  }, []); // Empty deps = run once on mount

  const handleSplitTeams = async () => {
    setLoadingTeams(true);
    try {
      const result = await splitTeams();
      setTeams(result);
    } catch {
      setError('Chia đội thất bại. Cần ít nhất vài thành viên.');
    } finally {
      setLoadingTeams(false);
    }
  };

  const handleLogout = () => {
    logout();         // Xóa token khỏi localStorage và store
    navigate('/login'); // Redirect về trang login
  };

  // Array để map màu theo index — tránh if/else lồng nhau
  const teamColors = ['text-blue-400', 'text-emerald-400', 'text-purple-400'];

  return (
    <div className="min-h-screen pb-24" style={{ backgroundColor: '#0f172a', color: '#f1f5f9' }}>

      {/* STICKY NAVBAR: position:sticky + top:0 → dính ở đầu khi scroll */}
      <nav className="sticky top-0 z-50 px-6 py-4 mx-4 mt-4 mb-8 flex justify-between items-center rounded-3xl"
        style={{ background: 'rgba(30,41,59,0.7)', backdropFilter: 'blur(12px)', border: '1px solid rgba(255,255,255,0.1)' }}>
        <div className="flex items-center gap-2">
          <div className="bg-blue-600 p-2 rounded-lg">
            <i className="fas fa-volleyball text-white text-xl"></i>
          </div>
          <span className="font-extrabold text-xl tracking-tight">
            VOLLEY<span className="text-blue-500">SQUAD</span>
          </span>
        </div>
        <div className="flex items-center gap-4">
          <button className="relative">
            <i className="far fa-bell text-xl text-slate-400"></i>
            <span className="absolute -top-1 -right-1 bg-red-500 w-2 h-2 rounded-full"></span>
          </button>
          <div className="flex items-center gap-2 border-l border-slate-700 pl-4">
            {/* Optional chaining: user?.name = nếu user null thì không throw, trả undefined
                Nullish coalescing: ?? 'User' = dùng 'User' nếu giá trị là null/undefined
                Khác || (OR): || dùng fallback cho cả falsy (0, '') còn ?? chỉ null/undefined */}
            <img
              src={`https://ui-avatars.com/api/?name=${encodeURIComponent(user?.name ?? 'User')}&background=random`}
              className="w-8 h-8 rounded-full border-2 border-blue-500"
              alt="User"
            />
            <span className="hidden md:block text-sm font-semibold">{user?.name ?? 'Member'}</span>
          </div>
          <button onClick={handleLogout} className="text-slate-500 hover:text-white text-sm">
            <i className="fas fa-sign-out-alt"></i>
          </button>
        </div>
      </nav>

      <div className="container mx-auto px-4 max-w-6xl">
        {error && (
          <div className="mb-4 p-3 bg-red-500/10 border border-red-500/20 rounded-xl text-red-400 text-sm">
            <i className="fas fa-exclamation-triangle mr-2"></i>{error}
          </div>
        )}

        {/* CSS GRID LAYOUT: grid-cols-1 (mobile) → lg:grid-cols-3 (desktop)
            Responsive Design: Tailwind breakpoint lg: = màn hình >= 1024px */}
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">

          {/* Cột trái: Match + Team Split */}
          <div className="lg:col-span-2 space-y-6">

            <MatchBanner
              match={currentMatch}
              onRegister={() => alert('Đã đăng ký slot!')}
              onSplitTeams={handleSplitTeams}
              loading={loadingTeams}
            />

            {/* TEAM SPLIT SECTION */}
            <div className="p-6 rounded-3xl" style={{ background: 'rgba(30,41,59,0.7)', backdropFilter: 'blur(12px)', border: '1px solid rgba(255,255,255,0.1)' }}>
              <div className="flex justify-between items-center mb-6">
                <h3 className="text-xl font-bold">
                  Chia Đội Dự Kiến{' '}
                  <span className="text-xs text-slate-500 font-normal ml-2">(Snake Draft)</span>
                </h3>
                <button
                  onClick={handleSplitTeams}
                  disabled={loadingTeams}
                  className="text-blue-400 text-sm hover:underline disabled:opacity-50"
                >
                  <i className="fas fa-sync-alt mr-1"></i> Xếp lại
                </button>
              </div>

              {/* Conditional rendering với ternary:
                  teams.length === 0 → hiển thị placeholder
                  teams.length > 0  → hiển thị danh sách đội */}
              {teams.length === 0 ? (
                <p className="text-slate-500 text-sm italic">Nhấn "Chia đội" để tự động phân team theo SkillPoint.</p>
              ) : (
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                  {/* Array.map(): Chuyển mảng data thành mảng JSX element.
                      key={team.teamName}: Bắt buộc khi render list.
                      React dùng key để xác định element nào thay đổi khi reconcile Virtual DOM.
                      Không dùng index làm key nếu list có thể reorder (gây bug render sai). */}
                  {teams.map((team, i) => (
                    <div key={team.teamName} className="bg-slate-800/50 rounded-2xl p-4 border border-slate-700">
                      <div className="flex justify-between items-center mb-4 border-b border-slate-700 pb-2">
                        <span className={`font-bold ${teamColors[i % 3]}`}>{team.teamName}</span>
                        <span className="text-xs font-bold text-slate-500">{team.totalSkillPoint} Pts</span>
                      </div>
                      <ul className="space-y-2">
                        {team.members.map((m) => (
                          <li key={m.id} className="flex justify-between text-sm text-slate-200">
                            <span>{m.name}</span>
                            <span className="text-slate-500">{m.skillPoint}</span>
                          </li>
                        ))}
                      </ul>
                    </div>
                  ))}
                </div>
              )}
            </div>

          </div>

          {/* Cột phải: PlayerCard + Leaderboard
              user && <PlayerCard>: Short-circuit evaluation
              Nếu user null (chưa load xong) → không render PlayerCard → không crash */}
          <div className="space-y-8">
            {user && <PlayerCard member={user} />}
            {members.length > 0 && <Leaderboard members={members} />}
          </div>

        </div>
      </div>

      {/* BOTTOM NAVIGATION (Mobile only): md:hidden = ẩn trên màn hình >= 768px */}
      <div className="md:hidden fixed bottom-4 left-4 right-4 p-4 flex justify-around items-center shadow-2xl z-50 rounded-3xl"
        style={{ background: 'rgba(30,41,59,0.9)', border: '1px solid rgba(255,255,255,0.1)' }}>
        <button className="text-blue-500"><i className="fas fa-home text-xl"></i></button>
        <button className="text-slate-500"><i className="fas fa-calendar-check text-xl"></i></button>
        <button className="bg-blue-600 w-12 h-12 rounded-full -mt-10 border-4 border-[#0f172a] shadow-lg flex items-center justify-center text-white">
          <i className="fas fa-plus"></i>
        </button>
        <button className="text-slate-500"><i className="fas fa-wallet text-xl"></i></button>
        <button className="text-slate-500"><i className="fas fa-user text-xl"></i></button>
      </div>

    </div>
  );
}


  return (
    <div className="min-h-screen pb-24" style={{ backgroundColor: '#0f172a', color: '#f1f5f9' }}>

      {/* Navbar */}
      <nav className="sticky top-0 z-50 px-6 py-4 mx-4 mt-4 mb-8 flex justify-between items-center rounded-3xl"
        style={{ background: 'rgba(30,41,59,0.7)', backdropFilter: 'blur(12px)', border: '1px solid rgba(255,255,255,0.1)' }}>
        <div className="flex items-center gap-2">
          <div className="bg-blue-600 p-2 rounded-lg">
            <i className="fas fa-volleyball text-white text-xl"></i>
          </div>
          <span className="font-extrabold text-xl tracking-tight">
            VOLLEY<span className="text-blue-500">SQUAD</span>
          </span>
        </div>
        <div className="flex items-center gap-4">
          <button className="relative">
            <i className="far fa-bell text-xl text-slate-400"></i>
            <span className="absolute -top-1 -right-1 bg-red-500 w-2 h-2 rounded-full"></span>
          </button>
          <div className="flex items-center gap-2 border-l border-slate-700 pl-4">
            <img
              src={`https://ui-avatars.com/api/?name=${encodeURIComponent(user?.name ?? 'User')}&background=random`}
              className="w-8 h-8 rounded-full border-2 border-blue-500"
              alt="User"
            />
            <span className="hidden md:block text-sm font-semibold">{user?.name ?? 'Member'}</span>
          </div>
          <button onClick={handleLogout} className="text-slate-500 hover:text-white text-sm">
            <i className="fas fa-sign-out-alt"></i>
          </button>
        </div>
      </nav>

      <div className="container mx-auto px-4 max-w-6xl">
        {error && (
          <div className="mb-4 p-3 bg-red-500/10 border border-red-500/20 rounded-xl text-red-400 text-sm">
            <i className="fas fa-exclamation-triangle mr-2"></i>{error}
          </div>
        )}

        <div className="grid grid-cols-1 lg:grid-cols-3 gap-8">

          {/* Cột trái: Match + Team Split */}
          <div className="lg:col-span-2 space-y-6">

            <MatchBanner
              match={currentMatch}
              onRegister={() => alert('Đã đăng ký slot!')}
              onSplitTeams={handleSplitTeams}
              loading={loadingTeams}
            />

            {/* Chia đội */}
            <div className="p-6 rounded-3xl" style={{ background: 'rgba(30,41,59,0.7)', backdropFilter: 'blur(12px)', border: '1px solid rgba(255,255,255,0.1)' }}>
              <div className="flex justify-between items-center mb-6">
                <h3 className="text-xl font-bold">
                  Chia Đội Dự Kiến{' '}
                  <span className="text-xs text-slate-500 font-normal ml-2">(Snake Draft)</span>
                </h3>
                <button
                  onClick={handleSplitTeams}
                  disabled={loadingTeams}
                  className="text-blue-400 text-sm hover:underline disabled:opacity-50"
                >
                  <i className="fas fa-sync-alt mr-1"></i> Xếp lại
                </button>
              </div>

              {teams.length === 0 ? (
                <p className="text-slate-500 text-sm italic">Nhấn "Chia đội" để tự động phân team theo SkillPoint.</p>
              ) : (
                <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
                  {teams.map((team, i) => (
                    <div key={team.teamName} className="bg-slate-800/50 rounded-2xl p-4 border border-slate-700">
                      <div className="flex justify-between items-center mb-4 border-b border-slate-700 pb-2">
                        <span className={`font-bold ${teamColors[i % 3]}`}>{team.teamName}</span>
                        <span className="text-xs font-bold text-slate-500">{team.totalSkillPoint} Pts</span>
                      </div>
                      <ul className="space-y-2">
                        {team.members.map((m) => (
                          <li key={m.id} className="flex justify-between text-sm text-slate-200">
                            <span>{m.name}</span>
                            <span className="text-slate-500">{m.skillPoint}</span>
                          </li>
                        ))}
                      </ul>
                    </div>
                  ))}
                </div>
              )}
            </div>

          </div>

          {/* Cột phải: PlayerCard + Leaderboard */}
          <div className="space-y-8">
            {user && <PlayerCard member={user} />}
            {members.length > 0 && <Leaderboard members={members} />}
          </div>

        </div>
      </div>

      {/* Bottom Mobile Menu */}
      <div className="md:hidden fixed bottom-4 left-4 right-4 p-4 flex justify-around items-center shadow-2xl z-50 rounded-3xl"
        style={{ background: 'rgba(30,41,59,0.9)', border: '1px solid rgba(255,255,255,0.1)' }}>
        <button className="text-blue-500"><i className="fas fa-home text-xl"></i></button>
        <button className="text-slate-500"><i className="fas fa-calendar-check text-xl"></i></button>
        <button className="bg-blue-600 w-12 h-12 rounded-full -mt-10 border-4 border-[#0f172a] shadow-lg flex items-center justify-center text-white">
          <i className="fas fa-plus"></i>
        </button>
        <button className="text-slate-500"><i className="fas fa-wallet text-xl"></i></button>
        <button className="text-slate-500"><i className="fas fa-user text-xl"></i></button>
      </div>

    </div>
  );
}
