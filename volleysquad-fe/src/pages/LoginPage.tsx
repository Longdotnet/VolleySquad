import { useState } from 'react';
import { useNavigate } from 'react-router-dom';
import { login } from '../api/authApi';
import { useAuthStore } from '../store/authStore';

export default function LoginPage() {
  const [username, setUsername] = useState('');
  const [error, setError] = useState('');
  const [loading, setLoading] = useState(false);
  const setAuth = useAuthStore((s) => s.setAuth);
  const navigate = useNavigate();

  const handleLogin = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    setError('');
    try {
      const data = await login(username);
      setAuth(data.token, data.member);
      navigate('/dashboard');
    } catch {
      setError('Tên đăng nhập không đúng hoặc server chưa chạy.');
    } finally {
      setLoading(false);
    }
  };

  return (
    <div className="min-h-screen flex items-center justify-center" style={{ backgroundColor: '#0f172a' }}>
      <div className="w-full max-w-sm p-8 rounded-3xl border border-white/10" style={{ background: 'rgba(30,41,59,0.7)', backdropFilter: 'blur(12px)' }}>
        {/* Logo */}
        <div className="flex items-center justify-center gap-2 mb-8">
          <div className="bg-blue-600 p-2 rounded-lg">
            <i className="fas fa-volleyball text-white text-xl"></i>
          </div>
          <span className="font-extrabold text-xl tracking-tight text-white">
            VOLLEY<span className="text-blue-500">SQUAD</span>
          </span>
        </div>

        <h2 className="text-2xl font-bold text-white text-center mb-1">Đăng nhập</h2>
        <p className="text-slate-400 text-sm text-center mb-6">Nhập tên thành viên để tiếp tục</p>

        <form onSubmit={handleLogin} className="space-y-4">
          <input
            type="text"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            placeholder="VD: Tài Admin"
            required
            className="w-full bg-slate-800 text-white border border-slate-700 rounded-xl px-4 py-3 text-sm focus:outline-none focus:border-blue-500 placeholder-slate-500"
          />

          {error && (
            <p className="text-red-400 text-xs text-center">{error}</p>
          )}

          <button
            type="submit"
            disabled={loading}
            className="w-full py-3 rounded-xl font-bold text-white text-sm disabled:opacity-60"
            style={{ background: 'linear-gradient(90deg, #3b82f6, #2563eb)' }}
          >
            {loading ? 'Đang đăng nhập...' : 'Đăng nhập'}
          </button>
        </form>

        <p className="text-slate-600 text-xs text-center mt-6">
          VolleySquad v1.0 — Quản lý bóng chuyền phong trào
        </p>
      </div>
    </div>
  );
}
