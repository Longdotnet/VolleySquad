import type { Member } from '../types/Member';

interface Props {
  member: Member;
}

export default function PlayerCard({ member }: Props) {
  return (
    <div
      className="w-full rounded-[30px] p-6 shadow-2xl relative overflow-hidden"
      style={{ background: 'linear-gradient(135deg, #fbbf24 0%, #f59e0b 100%)', color: '#000' }}
    >
      {/* Shine overlay */}
      <div
        className="absolute pointer-events-none"
        style={{
          top: '-50%', left: '-50%', width: '200%', height: '200%',
          background: 'rgba(255,255,255,0.15)', transform: 'rotate(30deg)',
        }}
      />

      <div className="flex justify-between items-start relative z-10">
        <div className="text-4xl font-black italic">{member.skillPoint}</div>
        <div className="text-xs font-bold bg-black/10 px-2 py-1 rounded">OH / ACE</div>
      </div>

      <div className="flex justify-center my-4 relative z-10">
        <img
          src={`https://ui-avatars.com/api/?name=${encodeURIComponent(member.name)}&size=128&background=000&color=fff`}
          className="w-24 h-24 rounded-full border-4 border-black/20"
          alt={member.name}
        />
      </div>

      <div className="text-center relative z-10">
        <h4 className="text-xl font-extrabold uppercase">{member.name}</h4>
        <div className="inline-block bg-black/10 text-xs font-bold px-3 py-1 rounded-full mt-1">
          {member.role}
        </div>
        <div className="flex justify-center gap-6 mt-4 text-xs font-bold">
          <div className="text-center">
            <p className="text-black/50 uppercase">Điểm KN</p>
            <p className="text-2xl font-black">{member.skillPoint}</p>
          </div>
          <div className="text-center">
            <p className="text-black/50 uppercase">Số dư</p>
            <p className="text-2xl font-black">{member.balance.toLocaleString('vi-VN')}đ</p>
          </div>
        </div>
      </div>
    </div>
  );
}
