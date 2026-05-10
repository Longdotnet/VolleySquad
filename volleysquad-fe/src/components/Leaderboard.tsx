import type { Member } from '../types/Member';

interface Props {
  members: Member[];
}

export default function Leaderboard({ members }: Props) {
  const sorted = [...members].sort((a, b) => b.skillPoint - a.skillPoint).slice(0, 5);

  return (
    <div className="p-6 rounded-3xl" style={{ background: 'rgba(30,41,59,0.7)', backdropFilter: 'blur(12px)', border: '1px solid rgba(255,255,255,0.1)' }}>
      <h3 className="text-xl font-bold mb-6 flex items-center gap-2 text-white">
        <i className="fas fa-trophy text-yellow-500"></i> Bảng Xếp Hạng
      </h3>
      <div className="space-y-3">
        {sorted.map((m, i) => (
          <div
            key={m.id}
            className={`flex items-center gap-3 p-3 rounded-2xl ${
              i === 0 ? 'bg-blue-500/10 border border-blue-500/20' : ''
            }`}
          >
            <span className={`font-bold w-5 text-sm ${
              i === 0 ? 'text-yellow-400' : i === 1 ? 'text-slate-300' : i === 2 ? 'text-amber-600' : 'text-slate-500'
            }`}>
              {i + 1}
            </span>
            <img
              src={`https://ui-avatars.com/api/?name=${encodeURIComponent(m.name)}&background=random`}
              className="w-9 h-9 rounded-full"
              alt={m.name}
            />
            <div className="flex-1">
              <p className="text-sm font-bold text-white">{m.name}</p>
              <p className="text-xs text-slate-500">{m.role}</p>
            </div>
            <span className={`font-bold text-sm ${
              i === 0 ? 'text-blue-400' : 'text-slate-300'
            }`}>
              {m.skillPoint} pts
            </span>
          </div>
        ))}
      </div>
    </div>
  );
}
