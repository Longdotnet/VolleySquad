// ============================================================
// LOGIN PAGE — LoginPage.tsx
// Trang xác thực với 3 tab: Đăng nhập (real) | Đăng ký (mock) | Quên mật khẩu (mock)
//
// Cấu trúc file:
//   1. Imports & CSS
//   2. Types & Interfaces
//   3. Constants (mock data)
//   4. Utility functions
//   5. Sub-components (nhỏ, tập trung, dễ test riêng)
//   6. Main LoginPage component
//
// CSS classes → src/styles/login.css
// Design tokens (màu, spacing, font) → src/styles/variables.css
// Animation keyframes → src/styles/animations.css
// ============================================================

import { useState, useEffect, useRef, useCallback } from 'react';
import { useNavigate } from 'react-router-dom';
import { login, register } from '../api/authApi';
import { useAuthStore } from '../store/authStore';
import { getPublicStats, getPublicActivities, type PublicStats, type PublicActivity } from '../api/publicApi';
import '../styles/login.css';

// ============================================================
// TYPES & INTERFACES
// ============================================================

type AuthTab      = 'login' | 'register' | 'forgot';
type ServerStatus = 'checking' | 'online' | 'offline';
type AvatarId     = 1 | 2 | 3 | 4 | 5 | 6;

interface RegisterFormData {
  name:            string;
  username:        string;
  password:        string;
  confirmPassword: string;
  avatarId:        AvatarId;
}

interface RegisterErrors {
  name?:            string;
  username?:        string;
  password?:        string;
  confirmPassword?: string;
}

interface DemoCredential {
  label:       string;
  username:    string;
  password:    string;
  description: string;
}

interface StatItem {
  icon:   string;
  value:  number;
  suffix: string;
  label:  string;
}

interface FeatureItem {
  icon:        string;
  title:       string;
  description: string;
}

// ActivityItem removed — replaced by PublicActivity from publicApi.ts

// ============================================================
// CONSTANTS — Mock data (đổi ở đây, không cần sửa component)
// ============================================================

const DEMO_CREDENTIALS: DemoCredential[] = [
  {
    label:       'Admin',
    username:    'Tài Admin',
    password:    'Admin@1234',
    description: 'Toàn quyền — quản lý trận, thành viên, ranking',
  },
  {
    label:       'Thành viên',
    username:    'Minh Tuấn',
    password:    'Member@123',
    description: 'Xem lịch, đặt slot, xem bảng xếp hạng',
  },
];

// MOCK_STATS removed — LeftPanel now fetches live data from /api/public/stats

const FEATURES: FeatureItem[] = [
  { icon: '🏐', title: 'Quản lý trận đấu',   description: 'Tạo, theo dõi và thống kê tất cả các trận bóng' },
  { icon: '⚡', title: 'Chia đội thông minh', description: 'Drag & drop chia đội cân bằng theo skill point' },
  { icon: '🏆', title: 'Bảng xếp hạng',      description: 'Cập nhật skill point real-time sau mỗi trận đấu' },
  { icon: '📊', title: 'Thống kê cá nhân',    description: 'Theo dõi phong độ và lịch sử thi đấu của bạn' },
  { icon: '👑', title: 'Quản lý thành viên',  description: 'Admin toàn quyền quản lý hội viên và quyền hạn' },
];

// ACTIVITY_FEED removed — ActivityTicker now receives live data from /api/public/activities

const AVATARS: string[]   = ['🦅', '🐯', '🦁', '🐺', '🦊', '🦈'];
const PASSWORD_MIN_LENGTH = 8;
const API_BASE            = (import.meta.env.VITE_API_URL as string | undefined) ?? 'https://localhost:7202/api';

// ============================================================
// UTILITY FUNCTIONS
// ============================================================

/**
 * Tính độ mạnh mật khẩu theo 5 tiêu chí.
 * Trả về score 0–5, label tiếng Việt và màu CSS.
 */
function getPasswordStrength(pwd: string): { score: number; label: string; color: string } {
  let score = 0;
  if (pwd.length >= 8)           score++;
  if (pwd.length >= 12)          score++;
  if (/[A-Z]/.test(pwd))         score++;
  if (/[0-9]/.test(pwd))         score++;
  if (/[^A-Za-z0-9]/.test(pwd))  score++;

  if (score <= 1) return { score, label: 'Rất yếu',    color: 'var(--vs-error)'   };
  if (score === 2) return { score, label: 'Yếu',        color: '#f97316'           };
  if (score === 3) return { score, label: 'Trung bình', color: 'var(--vs-warning)' };
  if (score === 4) return { score, label: 'Mạnh',       color: '#84cc16'           };
  return               { score: 5, label: 'Rất mạnh',   color: 'var(--vs-success)' };
}

/**
 * Validate toàn bộ register form.
 * Trả về object errors — rỗng = hợp lệ.
 */
function validateRegisterForm(data: RegisterFormData): RegisterErrors {
  const errors: RegisterErrors = {};

  if (!data.name.trim() || data.name.trim().length < 2) {
    errors.name = 'Tên phải có ít nhất 2 ký tự';
  }
  if (!data.username.trim() || data.username.trim().length < 3) {
    errors.username = 'Username phải có ít nhất 3 ký tự';
  }
  if (data.password.length < PASSWORD_MIN_LENGTH) {
    errors.password = `Mật khẩu phải có ít nhất ${PASSWORD_MIN_LENGTH} ký tự`;
  }
  if (data.password !== data.confirmPassword) {
    errors.confirmPassword = 'Mật khẩu xác nhận không khớp';
  }

  return errors;
}

// ============================================================
// SUB-COMPONENTS
// ============================================================

// ------------------------------------------------------------
// AnimatedCounter — Đếm từ 0 → target với cubic ease-out
// Dùng requestAnimationFrame (không block render thread)
// ------------------------------------------------------------
function AnimatedCounter({ target, suffix }: { target: number; suffix: string }) {
  const [value, setValue] = useState(0);
  const rafRef            = useRef<number>(0);

  useEffect(() => {
    const DURATION  = 1600;
    const startTime = performance.now();

    const step = (now: number) => {
      const elapsed  = now - startTime;
      const progress = Math.min(elapsed / DURATION, 1);
      const eased    = 1 - Math.pow(1 - progress, 3); // cubic ease-out
      setValue(Math.floor(eased * target));
      if (progress < 1) rafRef.current = requestAnimationFrame(step);
    };

    rafRef.current = requestAnimationFrame(step);
    return () => cancelAnimationFrame(rafRef.current);
  }, [target]);

  return <span>{value}{suffix}</span>;
}

// ------------------------------------------------------------
// PasswordStrengthMeter — 5 bar segments + label màu
// ------------------------------------------------------------
function PasswordStrengthMeter({ password }: { password: string }) {
  if (!password) return null;

  const { score, label, color } = getPasswordStrength(password);

  return (
    <div className="login-strength-meter">
      <div className="login-strength-bars">
        {[1, 2, 3, 4, 5].map((s) => (
          <div
            key={s}
            className="login-strength-bar"
            style={{ backgroundColor: s <= score ? color : undefined }}
          />
        ))}
      </div>
      <span className="login-strength-label" style={{ color }}>
        {label}
      </span>
    </div>
  );
}

// ------------------------------------------------------------
// AvatarPicker — Chọn avatar emoji cho tài khoản mới
// ------------------------------------------------------------
function AvatarPicker({
  selected,
  onChange,
}: {
  selected: AvatarId;
  onChange: (id: AvatarId) => void;
}) {
  return (
    <div className="login-avatar-grid">
      {AVATARS.map((emoji, idx) => {
        const id = (idx + 1) as AvatarId;
        return (
          <button
            key={id}
            type="button"
            className={`login-avatar-item ${selected === id ? 'selected' : ''}`}
            onClick={() => onChange(id)}
            aria-label={`Chọn avatar ${id}`}
          >
            {emoji}
          </button>
        );
      })}
    </div>
  );
}

// ------------------------------------------------------------
// ServerStatusBar — Hiển thị trạng thái kết nối server
// ------------------------------------------------------------
function ServerStatusBar({ status }: { status: ServerStatus }) {
  const config: Record<ServerStatus, { label: string; dotClass: string }> = {
    checking: { label: 'Đang kiểm tra server...',  dotClass: 'checking' },
    online:   { label: 'Server online',            dotClass: 'online'   },
    offline:  { label: 'Server offline',           dotClass: 'offline'  },
  };

  const { label, dotClass } = config[status];

  return (
    <div className="login-status-bar" role="status" aria-live="polite">
      <span className={`login-status-dot ${dotClass}`} />
      <span className="login-status-text">{label}</span>
      <span className="login-status-divider">•</span>
      <span className="login-status-text">VolleySquad v1.0</span>
      <span className="login-status-divider">•</span>
    </div>
  );
}

// ------------------------------------------------------------
// ActivityTicker — Cuộn marquee hoạt động gần đây (live data)
// Nhân đôi mảng để tạo vòng lặp liền mạch không bị giật
// ------------------------------------------------------------
function ActivityTicker({ activities }: { activities: PublicActivity[] }) {
  // Nếu chưa có data, fallback placeholder để CSS animation không bị trống
  const feed = activities.length > 0 ? activities : [
    { message: 'Đang tải hoạt động...', occurredAt: new Date().toISOString(), eventType: '' },
  ];
  // Nhân đôi để tạo seamless loop
  const items = [...feed, ...feed];

  return (
    <div className="login-ticker-wrap" aria-label="Hoạt động gần đây">
      <div className="login-ticker-label">🔴 LIVE</div>
      <div className="login-ticker-track">
        <div className="login-ticker-inner">
          {items.map((item, idx) => (
            <span key={idx} className="login-ticker-item">
              <span className="login-ticker-msg">{item.message}</span>
              <span className="login-ticker-time">
                {new Date(item.occurredAt).toLocaleString('vi-VN', {
                  hour: '2-digit', minute: '2-digit', day: '2-digit', month: '2-digit',
                })}
              </span>
            </span>
          ))}
        </div>
      </div>
    </div>
  );
}

// ------------------------------------------------------------
// LeftPanel — Brand, Stats (live), Features, ActivityTicker (live)
// Fetch /api/public/stats and /api/public/activities on mount.
// Ẩn trên mobile (CSS media query)
// ------------------------------------------------------------
function LeftPanel() {
  const [stats, setStats] = useState<PublicStats | null>(null);
  const [activities, setActivities] = useState<PublicActivity[]>([]);

  useEffect(() => {
    // Fetch stats — không block UI nếu lỗi
    getPublicStats()
      .then(setStats)
      .catch(() => { /* giữ null, hiển thị fallback */ });

    // Fetch activities
    getPublicActivities(25)
      .then(setActivities)
      .catch(() => { /* giữ [] */ });
  }, []);

  // Map stats từ API sang StatItem[] để AnimatedCounter hoạt động
  const statItems: StatItem[] = stats
    ? [
        { icon: '👥', value: stats.memberCount,       suffix: '',   label: 'Thành viên'    },
        { icon: '🏐', value: stats.matchesPlayed,      suffix: '',   label: 'Trận đã chơi'  },
        { icon: '⭐', value: stats.highestSkillPoint,  suffix: 'sp', label: 'Skill cao nhất' },
        { icon: '📅', value: stats.matchesThisMonth,   suffix: '',   label: 'Trận tháng này' },
      ]
    : [
        // Skeleton fallback khi đang load
        { icon: '👥', value: 0, suffix: '', label: 'Thành viên'    },
        { icon: '🏐', value: 0, suffix: '', label: 'Trận đã chơi'  },
        { icon: '⭐', value: 0, suffix: 'sp', label: 'Skill cao nhất' },
        { icon: '📅', value: 0, suffix: '', label: 'Trận tháng này' },
      ];

  return (
    <aside className="login-panel-left" aria-label="Giới thiệu VolleySquad">
      <div className="login-orb login-orb--1" aria-hidden="true" />
      <div className="login-orb login-orb--2" aria-hidden="true" />
      <div className="login-orb login-orb--3" aria-hidden="true" />

      <div className="login-brand">
        <span className="login-brand-icon" aria-hidden="true">🏐</span>
        <div>
          <div className="login-brand-name">VOLLEY<span>SQUAD</span></div>
          <div className="login-brand-tagline">Quản lý bóng chuyền phong trào</div>
        </div>
      </div>

      <div className="login-stats-grid" aria-label="Thống kê hệ thống">
        {statItems.map((stat) => (
          <div key={stat.label} className={`login-stat-card${!stats ? ' login-stat-card--loading' : ''}`}>
            <span className="login-stat-icon" aria-hidden="true">{stat.icon}</span>
            <span className="login-stat-value">
              <AnimatedCounter target={stat.value} suffix={stat.suffix} />
            </span>
            <span className="login-stat-label">{stat.label}</span>
          </div>
        ))}
      </div>

      <div className="login-features-list" aria-label="Tính năng">
        {FEATURES.map((f) => (
          <div key={f.title} className="login-feature-item">
            <span className="login-feature-icon" aria-hidden="true">{f.icon}</span>
            <div>
              <div className="login-feature-title">{f.title}</div>
              <div className="login-feature-desc">{f.description}</div>
            </div>
          </div>
        ))}
      </div>

      <ActivityTicker activities={activities} />
    </aside>
  );
}

// ------------------------------------------------------------
// DemoCredentials — Quick-fill buttons (không phải submit)
// Giúp demo dễ hơn, không yêu cầu gõ credential
// ------------------------------------------------------------
function DemoCredentials({
  onFill,
}: {
  onFill: (username: string, password: string) => void;
}) {
  return (
    <div className="login-demo-section">
      <div className="login-demo-label">Tài khoản demo</div>
      <div className="login-demo-grid">
        {DEMO_CREDENTIALS.map((c) => (
          <button
            key={c.label}
            type="button"
            className="login-demo-btn"
            onClick={() => onFill(c.username, c.password)}
            title={c.description}
          >
            <span className="login-demo-role">{c.label}</span>
            <span className="login-demo-user">@{c.username}</span>
          </button>
        ))}
      </div>
    </div>
  );
}

// ============================================================
// LOGIN FORM — Kết nối API thật
// ============================================================

function LoginForm() {
  const [username,     setUsername]     = useState('');
  const [password,     setPassword]     = useState('');
  const [error,        setError]        = useState('');
  const [loading,      setLoading]      = useState(false);
  const [showPassword, setShowPassword] = useState(false);
  const [rememberMe,   setRememberMe]   = useState(false);
  const [capsLock,     setCapsLock]     = useState(false);
  const [shake,        setShake]        = useState(false);

  const setAuth  = useAuthStore((s) => s.setAuth);
  const navigate = useNavigate();

  // Khôi phục username đã nhớ khi mount
  useEffect(() => {
    const saved = localStorage.getItem('vs_remembered_username');
    if (saved) {
      setUsername(saved);
      setRememberMe(true);
    }
  }, []);

  const handleKeyUp = (e: React.KeyboardEvent) => {
    setCapsLock(e.getModifierState('CapsLock'));
  };

  const handleFill = useCallback((u: string, p: string) => {
    setUsername(u);
    setPassword(p);
    setError('');
  }, []);

  const triggerShake = () => {
    setShake(true);
    setTimeout(() => setShake(false), 600);
  };

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setError('');
    setLoading(true);

    try {
      const data = await login(username, password);

      if (rememberMe) {
        localStorage.setItem('vs_remembered_username', username);
      } else {
        localStorage.removeItem('vs_remembered_username');
      }

      setAuth(data.token, data.member);
      navigate('/dashboard');
    } catch {
      // ⚠️ SECURITY: Message mơ hồ cố ý — tránh username enumeration attack.
      setError('Thông tin đăng nhập không đúng hoặc server chưa chạy.');
      triggerShake();
    } finally {
      setLoading(false);
    }
  };

  return (
    <form
      className={`login-form ${shake ? 'shake' : ''}`}
      onSubmit={handleSubmit}
      noValidate
    >
      <div className="login-field">
        <label className="login-label" htmlFor="login-username">Tên đăng nhập</label>
        <div className="login-input-wrap">
          <span className="login-input-icon" aria-hidden="true">👤</span>
          <input
            id="login-username"
            className="login-input"
            type="text"
            value={username}
            onChange={(e) => setUsername(e.target.value)}
            placeholder="VD: Tài Admin"
            autoComplete="username"
            required
          />
        </div>
      </div>

      <div className="login-field">
        <div className="login-label-row">
          <label className="login-label" htmlFor="login-password">Mật khẩu</label>
          {capsLock && (
            <span className="login-capslock-warn" role="alert">⚠ Caps Lock bật</span>
          )}
        </div>
        <div className="login-input-wrap">
          <span className="login-input-icon" aria-hidden="true">🔑</span>
          <input
            id="login-password"
            className="login-input"
            type={showPassword ? 'text' : 'password'}
            value={password}
            onChange={(e) => setPassword(e.target.value)}
            onKeyUp={handleKeyUp}
            placeholder="Mật khẩu"
            autoComplete="current-password"
            minLength={PASSWORD_MIN_LENGTH}
            required
          />
          <button
            type="button"
            className="login-input-action"
            onClick={() => setShowPassword((v) => !v)}
            tabIndex={-1}
            aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
          >
            {showPassword ? '🙈' : '👁️'}
          </button>
        </div>
      </div>

      <div className="login-options-row">
        <label className="login-remember">
          <input
            type="checkbox"
            checked={rememberMe}
            onChange={(e) => setRememberMe(e.target.checked)}
          />
          <span>Ghi nhớ đăng nhập</span>
        </label>
      </div>

      {error && (
        <div className="login-error-banner" role="alert">
          <span aria-hidden="true">⚠</span>
          <span>{error}</span>
        </div>
      )}

      <DemoCredentials onFill={handleFill} />

      <button
        type="submit"
        disabled={loading}
        className={`login-btn-primary ${loading ? 'loading' : ''}`}
      >
        {loading ? (
          <>
            <span className="login-spinner" aria-hidden="true" />
            Đang đăng nhập...
          </>
        ) : (
          'Đăng nhập'
        )}
      </button>
    </form>
  );
}

// ============================================================
// REGISTER FORM — Mock (không gọi API thật)
// Mô phỏng đầy đủ: validate, async username check, success state
// ============================================================

function RegisterForm() {
  const [formData, setFormData] = useState<RegisterFormData>({
    name:            '',
    username:        '',
    password:        '',
    confirmPassword: '',
    avatarId:        1,
  });
  const [errors,            setErrors]            = useState<RegisterErrors>({});
  const [loading,           setLoading]           = useState(false);
  const [success,           setSuccess]           = useState(false);
  const [showPassword,      setShowPassword]      = useState(false);
  const [checkingUsername,  setCheckingUsername]  = useState(false);
  const [usernameAvailable, setUsernameAvailable] = useState<boolean | null>(null);

  const usernameTimerRef = useRef<ReturnType<typeof setTimeout> | null>(null);

  const update = <K extends keyof RegisterFormData>(key: K, value: RegisterFormData[K]) => {
    setFormData((prev) => ({ ...prev, [key]: value }));
    setErrors((prev)   => ({ ...prev, [key]: undefined }));
  };

  // Mock async username availability check (debounce 800ms)
  useEffect(() => {
    if (usernameTimerRef.current) clearTimeout(usernameTimerRef.current);

    if (formData.username.trim().length < 3) {
      setUsernameAvailable(null);
      setCheckingUsername(false);
      return;
    }

    setCheckingUsername(true);
    setUsernameAvailable(null);

    usernameTimerRef.current = setTimeout(() => {
      const TAKEN = ['admin', 'tài admin', 'test', 'user', 'root'];
      const taken = TAKEN.includes(formData.username.trim().toLowerCase());
      setUsernameAvailable(!taken);
      setCheckingUsername(false);
    }, 800);

    return () => {
      if (usernameTimerRef.current) clearTimeout(usernameTimerRef.current);
    };
  }, [formData.username]);

  const [apiError, setApiError] = useState('');

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setApiError('');

    const errs = validateRegisterForm(formData);
    if (Object.keys(errs).length > 0) {
      setErrors(errs);
      return;
    }
    if (usernameAvailable === false) return;

    setLoading(true);
    try {
      // Dùng name là username (backend tìm kiếm theo Name)
      await register(formData.name.trim(), formData.password);
      setSuccess(true);
    } catch (err: unknown) {
      const msg = (err as { response?: { data?: string } })?.response?.data;
      setApiError(msg ?? 'Đăng ký thất bại. Vui lòng thử lại.');
    } finally {
      setLoading(false);
    }
  };

  const handleReset = () => {
    setSuccess(false);
    setFormData({ name: '', username: '', password: '', confirmPassword: '', avatarId: 1 });
    setErrors({});
    setUsernameAvailable(null);
  };

  if (success) {
    return (
      <div className="login-success-state">
        <div className="login-success-icon">✅</div>
        <h3 className="login-success-title">Yêu cầu đã gửi!</h3>
        <p className="login-success-desc">
          Tài khoản <strong>{formData.name}</strong> đã được tạo.
          <br />
          Vui lòng liên hệ Admin để kích hoạt — thường trong vòng 24 giờ.
        </p>
        <button type="button" className="login-btn-secondary" onClick={handleReset}>
          Đăng ký tài khoản khác
        </button>
      </div>
    );
  }

  return (
    <form className="login-form" onSubmit={handleSubmit} noValidate>
      <div className="login-field">
        <label className="login-label">Chọn avatar</label>
        <AvatarPicker selected={formData.avatarId} onChange={(id) => update('avatarId', id)} />
      </div>

      <div className="login-field">
        <label className="login-label" htmlFor="reg-name">Họ tên</label>
        <div className="login-input-wrap">
          <span className="login-input-icon" aria-hidden="true">✏️</span>
          <input
            id="reg-name"
            className={`login-input ${errors.name ? 'error' : ''}`}
            type="text"
            value={formData.name}
            onChange={(e) => update('name', e.target.value)}
            placeholder="VD: Nguyễn Văn A"
            autoComplete="name"
          />
        </div>
        {errors.name && <span className="login-field-error" role="alert">{errors.name}</span>}
      </div>

      <div className="login-field">
        <label className="login-label" htmlFor="reg-username">Tên đăng nhập</label>
        <div className="login-input-wrap">
          <span className="login-input-icon" aria-hidden="true">👤</span>
          <input
            id="reg-username"
            className={`login-input ${errors.username || usernameAvailable === false ? 'error' : ''}`}
            type="text"
            value={formData.username}
            onChange={(e) => update('username', e.target.value)}
            placeholder="VD: minhtuan123"
            autoComplete="username"
          />
          {checkingUsername && (
            <span className="login-input-action" aria-label="Đang kiểm tra...">⏳</span>
          )}
          {!checkingUsername && usernameAvailable === true && (
            <span className="login-input-action" aria-label="Khả dụng" style={{ color: 'var(--vs-success)' }}>✓</span>
          )}
          {!checkingUsername && usernameAvailable === false && (
            <span className="login-input-action" aria-label="Đã tồn tại" style={{ color: 'var(--vs-error)' }}>✗</span>
          )}
        </div>
        {usernameAvailable === false && (
          <span className="login-field-error" role="alert">Username đã tồn tại, vui lòng chọn tên khác</span>
        )}
        {errors.username && <span className="login-field-error" role="alert">{errors.username}</span>}
      </div>

      <div className="login-field">
        <label className="login-label" htmlFor="reg-password">Mật khẩu</label>
        <div className="login-input-wrap">
          <span className="login-input-icon" aria-hidden="true">🔑</span>
          <input
            id="reg-password"
            className={`login-input ${errors.password ? 'error' : ''}`}
            type={showPassword ? 'text' : 'password'}
            value={formData.password}
            onChange={(e) => update('password', e.target.value)}
            placeholder={`Ít nhất ${PASSWORD_MIN_LENGTH} ký tự`}
            autoComplete="new-password"
          />
          <button
            type="button"
            className="login-input-action"
            onClick={() => setShowPassword((v) => !v)}
            tabIndex={-1}
            aria-label={showPassword ? 'Ẩn mật khẩu' : 'Hiện mật khẩu'}
          >
            {showPassword ? '🙈' : '👁️'}
          </button>
        </div>
        <PasswordStrengthMeter password={formData.password} />
        {errors.password && <span className="login-field-error" role="alert">{errors.password}</span>}
      </div>

      <div className="login-field">
        <label className="login-label" htmlFor="reg-confirm">Xác nhận mật khẩu</label>
        <div className="login-input-wrap">
          <span className="login-input-icon" aria-hidden="true">🔒</span>
          <input
            id="reg-confirm"
            className={`login-input ${errors.confirmPassword ? 'error' : ''}`}
            type="password"
            value={formData.confirmPassword}
            onChange={(e) => update('confirmPassword', e.target.value)}
            placeholder="Nhập lại mật khẩu"
            autoComplete="new-password"
          />
        </div>
        {errors.confirmPassword && (
          <span className="login-field-error" role="alert">{errors.confirmPassword}</span>
        )}
      </div>

      <div className="login-register-notice">
        ℹ️ Sau khi đăng ký, Admin sẽ xét duyệt và kích hoạt tài khoản trong vòng 24 giờ.
      </div>

      {apiError && (
        <div className="login-error-banner" role="alert">
          ⚠️ {apiError}
        </div>
      )}

      <button
        type="submit"
        disabled={loading || usernameAvailable === false}
        className={`login-btn-primary ${loading ? 'loading' : ''}`}
      >
        {loading ? (
          <>
            <span className="login-spinner" aria-hidden="true" />
            Đang gửi yêu cầu...
          </>
        ) : (
          'Gửi yêu cầu đăng ký'
        )}
      </button>
    </form>
  );
}

// ============================================================
// FORGOT PASSWORD FORM — Mock (gửi email giả + countdown)
// ============================================================

function ForgotPasswordForm() {
  const [email,     setEmail]     = useState('');
  const [loading,   setLoading]   = useState(false);
  const [sent,      setSent]      = useState(false);
  const [countdown, setCountdown] = useState(0);

  const intervalRef = useRef<ReturnType<typeof setInterval> | null>(null);

  const startCountdown = (seconds = 60) => {
    setCountdown(seconds);
    intervalRef.current = setInterval(() => {
      setCountdown((prev) => {
        if (prev <= 1) {
          clearInterval(intervalRef.current!);
          return 0;
        }
        return prev - 1;
      });
    }, 1000);
  };

  useEffect(() => {
    return () => {
      if (intervalRef.current) clearInterval(intervalRef.current);
    };
  }, []);

  const handleSubmit = async (e: React.FormEvent) => {
    e.preventDefault();
    setLoading(true);
    await new Promise<void>((resolve) => setTimeout(resolve, 1800)); // mock
    setLoading(false);
    setSent(true);
    startCountdown(60);
  };

  const handleResend = async () => {
    if (countdown > 0 || loading) return;
    setLoading(true);
    await new Promise<void>((resolve) => setTimeout(resolve, 1500)); // mock
    setLoading(false);
    startCountdown(60);
  };

  if (sent) {
    return (
      <div className="login-success-state">
        <div className="login-success-icon">📧</div>
        <h3 className="login-success-title">Email đã được gửi!</h3>
        <p className="login-success-desc">
          Link đặt lại mật khẩu đã gửi tới <strong>{email}</strong>.
          <br />
          Kiểm tra hộp thư đến và thư mục Spam.
        </p>
        <button
          type="button"
          className="login-btn-secondary"
          onClick={handleResend}
          disabled={countdown > 0 || loading}
        >
          {loading
            ? 'Đang gửi lại...'
            : countdown > 0
            ? `Gửi lại sau ${countdown}s`
            : 'Gửi lại email'}
        </button>
      </div>
    );
  }

  return (
    <form className="login-form" onSubmit={handleSubmit} noValidate>
      <div className="login-forgot-info">
        <p>
          Nhập email đã đăng ký. Chúng tôi sẽ gửi link đặt lại mật khẩu tới địa chỉ đó.
        </p>
        <p className="login-forgot-note">
          ⚠ Tính năng mô phỏng — email thực tế sẽ không được gửi trong môi trường demo.
        </p>
      </div>

      <div className="login-field">
        <label className="login-label" htmlFor="forgot-email">Địa chỉ email</label>
        <div className="login-input-wrap">
          <span className="login-input-icon" aria-hidden="true">📧</span>
          <input
            id="forgot-email"
            className="login-input"
            type="email"
            value={email}
            onChange={(e) => setEmail(e.target.value)}
            placeholder="email@example.com"
            autoComplete="email"
            required
          />
        </div>
      </div>

      <button
        type="submit"
        disabled={loading || !email.trim()}
        className={`login-btn-primary ${loading ? 'loading' : ''}`}
      >
        {loading ? (
          <>
            <span className="login-spinner" aria-hidden="true" />
            Đang gửi...
          </>
        ) : (
          'Gửi link đặt lại mật khẩu'
        )}
      </button>
    </form>
  );
}

// ============================================================
// MAIN COMPONENT
// ============================================================

export default function LoginPage() {
  const [activeTab,    setActiveTab]    = useState<AuthTab>('login');
  const [serverStatus, setServerStatus] = useState<ServerStatus>('checking');

  // Ping /health để kiểm tra server — tự động lặp mỗi 30 giây
  useEffect(() => {
    const healthUrl = API_BASE.replace(/\/api\/?$/, '/health');

    const checkHealth = async () => {
      setServerStatus('checking');
      try {
        const res = await fetch(healthUrl, { signal: AbortSignal.timeout(5000) });
        setServerStatus(res.ok ? 'online' : 'offline');
      } catch {
        setServerStatus('offline');
      }
    };

    checkHealth();
    const timer = setInterval(checkHealth, 30_000);
    return () => clearInterval(timer);
  }, []);

  const tabs: { id: AuthTab; icon: string; label: string }[] = [
    { id: 'login',    icon: '🔐', label: 'Đăng nhập'     },
    { id: 'register', icon: '✍️', label: 'Đăng ký'       },
    { id: 'forgot',   icon: '❓', label: 'Quên mật khẩu' },
  ];

  return (
    <div className="login-root">
      {/* Floating volleyball particles (background decoration) */}
      <div className="login-particles" aria-hidden="true">
        {Array.from({ length: 8 }).map((_, i) => (
          <span key={i} className={`login-particle login-particle--${i + 1}`}>🏐</span>
        ))}
      </div>

      <div className="login-container">
        {/* Left brand panel — ẩn trên mobile */}
        <LeftPanel />

        {/* Right auth area */}
        <main className="login-panel-right">
          <div className="login-form-card">
            {/* Mobile-only brand header */}
            <div className="login-card-header">
              <div className="login-mobile-brand" aria-hidden="true">
                <span>🏐</span>
                <span>VOLLEY<strong>SQUAD</strong></span>
              </div>
            </div>

            {/* Tab switcher */}
            <div className="login-tabs" role="tablist" aria-label="Lựa chọn xác thực">
              {tabs.map((tab) => (
                <button
                  key={tab.id}
                  role="tab"
                  aria-selected={activeTab === tab.id}
                  aria-controls={`tabpanel-${tab.id}`}
                  className={`login-tab ${activeTab === tab.id ? 'active' : ''}`}
                  onClick={() => setActiveTab(tab.id)}
                >
                  <span className="login-tab-icon" aria-hidden="true">{tab.icon}</span>
                  <span className="login-tab-label">{tab.label}</span>
                </button>
              ))}
            </div>

            {/* key prop → remount on tab change → fresh entry animation */}
            <div
              id={`tabpanel-${activeTab}`}
              role="tabpanel"
              className="login-tab-content"
              key={activeTab}
            >
              {activeTab === 'login'    && <LoginForm />}
              {activeTab === 'register' && <RegisterForm />}
              {activeTab === 'forgot'   && <ForgotPasswordForm />}
            </div>
          </div>

          <ServerStatusBar status={serverStatus} />
        </main>
      </div>
    </div>
  );
}
