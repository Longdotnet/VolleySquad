import type { Match } from '../types/Match';

interface Props {
  match: Match;
  onRegister: () => void;
  onSplitTeams: () => void;
  loading?: boolean;
}

export default function MatchBanner({ match, onRegister, onSplitTeams, loading }: Props) {
  const registered = match.registeredMemberIds.length;
  const pct = Math.round((registered / match.maxSlots) * 100);
  const playDate = new Date(match.playDate).toLocaleDateString('vi-VN', {
    weekday: 'long', day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit',
  });

  return (
    <div className="p-6 rounded-3xl border-l-4 border-blue-500" style={{ background: 'rgba(30,41,59,0.7)', backdropFilter: 'blur(12px)', border: '1px solid rgba(255,255,255,0.1)', borderLeft: '4px solid #3b82f6' }}>
      <div className="flex flex-col md:flex-row justify-between gap-4">
        <div>
          <span className="text-blue-400 text-xs font-bold uppercase tracking-wider bg-blue-500/20 px-2 py-1 rounded-full">
            Trận đấu tiếp theo
          </span>
          <h2 className="text-3xl font-bold mt-2 text-white">{match.location}</h2>
          <p className="text-slate-400 mt-1 text-sm">
            <i className="far fa-calendar-alt mr-2"></i>{playDate}
          </p>
        </div>
        <div className="text-right">
          <div className="text-sm text-slate-400">Trạng thái Slot</div>
          <div className="text-2xl font-bold text-emerald-400">
            {registered} / {match.maxSlots}{' '}
            <span className="text-sm text-slate-500 font-normal">đã đăng ký</span>
          </div>
        </div>
      </div>

      {/* Progress bar */}
      <div className="w-full bg-slate-800 h-3 rounded-full mt-6 overflow-hidden">
        <div
          className="bg-emerald-500 h-full rounded-full"
          style={{ width: `${pct}%`, boxShadow: '0 0 10px rgba(16,185,129,0.5)' }}
        />
      </div>

      <div className="grid grid-cols-2 md:grid-cols-3 gap-4 mt-8">
        <button
          onClick={onRegister}
          disabled={loading}
          className="py-3 px-4 rounded-xl font-bold flex items-center justify-center gap-2 text-white disabled:opacity-60"
          style={{ background: 'linear-gradient(90deg,#3b82f6,#2563eb)' }}
        >
          <i className="fas fa-plus-circle"></i> Đăng ký ngay
        </button>
        <button className="bg-slate-800 hover:bg-slate-700 py-3 px-4 rounded-xl font-bold text-white flex items-center justify-center gap-2">
          <i className="fas fa-share-alt"></i> Mời bạn
        </button>
        <button
          onClick={onSplitTeams}
          className="bg-blue-500/10 text-blue-400 border border-blue-500/20 py-3 px-4 rounded-xl font-bold flex items-center justify-center gap-2"
        >
          <i className="fas fa-layer-group"></i> Chia đội
        </button>
      </div>
    </div>
  );
}
