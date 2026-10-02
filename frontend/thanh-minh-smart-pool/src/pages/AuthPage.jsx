import { useState } from 'react'
import { Link } from 'react-router-dom'
import './AuthPage.css'

export default function AuthPage() {
  const [tab, setTab] = useState('login')
  const [showPw, setShowPw] = useState(false)

  const isRegister = tab === 'register'

  return (
    <div className="auth-stage">
      {/* Left: Form */}
      <section className="auth-pane">
        <div className="auth-pane-logo">
          <div className="auth-pane-logo-inner">
            <div className="auth-logo-icon">
              <svg viewBox="0 0 24 24" fill="white"><path d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z"/></svg>
            </div>
            <div>
              <div className="auth-logo-name">Thành Minh</div>
              <div className="auth-logo-sub">Bể bơi thông minh</div>
            </div>
          </div>
          <Link to="/" className="auth-back-btn">
            <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="2.2" width="14" height="14">
              <path d="M12 4l-6 6 6 6" strokeLinecap="round" strokeLinejoin="round"/>
            </svg>
            Trang chủ
          </Link>
        </div>

        <div className="auth-form-wave" aria-hidden="true">
          <svg className="fw1" viewBox="0 0 1440 120" preserveAspectRatio="none">
            <path d="M0,60 C360,120 1080,0 1440,60 L1440,120 L0,120 Z" fill="rgba(0,119,182,0.06)"/>
          </svg>
          <svg className="fw2" viewBox="0 0 1440 120" preserveAspectRatio="none">
            <path d="M0,80 C480,20 960,120 1440,40 L1440,120 L0,120 Z" fill="rgba(0,180,216,0.05)"/>
          </svg>
        </div>

        <div className="auth-card" id="auth-card">
          {/* Tabs */}
          <div className="auth-tabs">
            <button
              className={`auth-tab${tab === 'login' ? ' active' : ''}`}
              onClick={() => setTab('login')} type="button"
            >Đăng nhập</button>
            <button
              className={`auth-tab${tab === 'register' ? ' active' : ''}`}
              onClick={() => setTab('register')} type="button"
            >Đăng ký</button>
          </div>

          <h1 className="auth-card-title">
            {isRegister ? 'Tạo tài khoản' : 'Chào mừng trở lại!'}
          </h1>
          <p className="auth-card-sub">
            {isRegister
              ? <><b>Đăng ký</b> để đặt vé và trải nghiệm đặc quyền thành viên.</>
              : <><b>Đăng nhập</b> để tiếp tục trải nghiệm bể bơi Thành Minh.</>
            }
          </p>

          {/* Name row (register) */}
          {isRegister && (
            <div className="auth-name-row">
              <div className="auth-field-group">
                <label className="auth-field-label" htmlFor="inp-fname">Họ</label>
                <div className="auth-field">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="18" height="18"><circle cx="12" cy="8" r="4"/><path d="M4 20c0-4 3.6-7 8-7"/></svg>
                  <input type="text" id="inp-fname" placeholder="Nguyễn" autoComplete="given-name" aria-label="Họ"/>
                </div>
              </div>
              <div className="auth-field-group">
                <label className="auth-field-label" htmlFor="inp-lname">Tên</label>
                <div className="auth-field">
                  <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="18" height="18"><circle cx="12" cy="8" r="4"/><path d="M12 15c4.4 0 8 3 8 7"/></svg>
                  <input type="text" id="inp-lname" placeholder="Văn An" autoComplete="family-name" aria-label="Tên"/>
                </div>
              </div>
            </div>
          )}

          {/* Email */}
          <div className="auth-field-group">
            <label className="auth-field-label" htmlFor="inp-email">Email</label>
            <div className="auth-field">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="18" height="18"><rect x="2" y="4" width="20" height="16" rx="2"/><path d="m2 7 10 7 10-7"/></svg>
              <input type="email" id="inp-email" placeholder="Eg. johndoe@gmail.com" autoComplete="email" aria-label="Địa chỉ email"/>
            </div>
          </div>

          {/* Password */}
          <div className="auth-field-group">
            <label className="auth-field-label" htmlFor="inp-pw">Mật khẩu</label>
            <div className="auth-field">
              <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="18" height="18"><rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>
              <input type={showPw ? 'text' : 'password'} id="inp-pw" placeholder="Mật khẩu"
                autoComplete={isRegister ? 'new-password' : 'current-password'} aria-label="Mật khẩu"/>
              <button className="auth-eye-btn" type="button" onClick={() => setShowPw(!showPw)} aria-label="Hiện/ẩn mật khẩu">
                {showPw
                  ? <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="18" height="18"><path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24"/><line x1="1" y1="1" x2="23" y2="23" stroke="currentColor" strokeWidth="2"/></svg>
                  : <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="18" height="18"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z"/><circle cx="12" cy="12" r="3"/></svg>
                }
              </button>
            </div>
          </div>

          {/* Confirm pw (register) */}
          {isRegister && (
            <div className="auth-field-group">
              <label className="auth-field-label" htmlFor="inp-pw2">Xác nhận mật khẩu</label>
              <div className="auth-field">
                <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="18" height="18"><path d="M9 12l2 2 4-4"/><rect x="3" y="11" width="18" height="11" rx="2"/><path d="M7 11V7a5 5 0 0 1 10 0v4"/></svg>
                <input type="password" id="inp-pw2" placeholder="Nhập lại mật khẩu" autoComplete="new-password" aria-label="Xác nhận mật khẩu"/>
              </div>
            </div>
          )}

          {/* Forgot */}
          {!isRegister && (
            <div className="auth-forgot-row">
              <Link to="#" className="auth-forgot-link">Quên mật khẩu?</Link>
            </div>
          )}

          {/* Main button */}
          <button className="auth-btn-main" id="loginBtn" type="button">
            <span>{isRegister ? 'Đăng ký' : 'Đăng nhập'}</span>
            <svg viewBox="0 0 22 22" fill="none" width="18" height="18">
              <path d="M3 11h15.4M11 3.3l7.7 7.7-7.7 7.7" stroke="#fff" strokeWidth="2.6" strokeLinecap="round" strokeLinejoin="round"/>
            </svg>
          </button>

          {/* Divider */}
          <div className="auth-divider">
            <i></i><b>HOẶC</b><i></i>
          </div>

          {/* Google */}
          <button className="auth-btn-google" id="gBtn" type="button">
            <svg viewBox="0 0 48 48" width="22" height="22">
              <path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z"/>
              <path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z"/>
              <path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z"/>
              <path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.18 1.48-4.97 2.31-8.16 2.31-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z"/>
            </svg>
            <span>{isRegister ? 'Đăng ký với Google' : 'Đăng nhập với Google'}</span>
          </button>

          {/* Footer */}
          <p className="auth-footer-text">
            {isRegister ? 'Đã có tài khoản? ' : 'Chưa có tài khoản? '}
            <button
              type="button"
              className="auth-footer-link"
              onClick={() => setTab(isRegister ? 'login' : 'register')}
            >
              {isRegister ? 'Đăng nhập' : 'Đăng ký miễn phí'}
            </button>
          </p>

          {isRegister && (
            <p className="auth-terms-text">
              Bằng cách đăng ký, bạn đồng ý với{' '}
              <Link to="#">Điều khoản dịch vụ</Link> và{' '}
              <Link to="#">Chính sách bảo mật</Link>.
            </p>
          )}
        </div>
      </section>

      {/* Right: Video/Photo */}
      <section className="auth-photo" aria-label="Hình ảnh bể bơi Thành Minh">
        <video className="auth-photo-img"
          autoPlay muted loop playsInline preload="auto">
          <source src="https://d8j0ntlcm91z4.cloudfront.net/user_38xzZboKViGWJOttwIXH07lWA1P/hf_20260424_064411_9e9d7f84-9277-41f4-ab10-59172d89e6be.mp4" type="video/mp4"/>
        </video>
        <div className="auth-photo-overlay" aria-hidden="true"></div>

        {/* Bubbles */}
        <div className="auth-bubbles" aria-hidden="true">
          {[...Array(6)].map((_,i) => <div key={i} className={`auth-bubble ab${i+1}`}/>)}
        </div>

        {/* Waves */}
        <div className="auth-waves-wrap" aria-hidden="true">
          <div className="auth-wave aw1">
            <svg viewBox="0 0 2880 120" preserveAspectRatio="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M0,60 C480,120 960,0 1440,60 C1920,120 2400,0 2880,60 L2880,120 L0,120 Z" fill="rgba(0,119,182,0.35)"/>
            </svg>
          </div>
          <div className="auth-wave aw2">
            <svg viewBox="0 0 2880 120" preserveAspectRatio="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M0,80 C480,20 960,100 1440,50 C1920,0 2400,90 2880,40 L2880,120 L0,120 Z" fill="rgba(0,180,216,0.25)"/>
            </svg>
          </div>
        </div>

        {/* Content */}
        <div className="auth-photo-content">
          <div className="auth-photo-badge">
            <svg viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2" width="16" height="16">
              <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>
            </svg>
            An toàn · Tiêu chuẩn · Chất lượng
          </div>
          <h2 className="auth-photo-headline">
            Trải nghiệm bơi lội<br/>
            <span>đẳng cấp</span> tại<br/>
            Thành Minh
          </h2>
          <p className="auth-photo-sub">
            Bể bơi hiện đại với camera AI giám sát an toàn 24/7, dịch vụ cho thuê phụ kiện và đồ ăn nhanh ngay tại chỗ.
          </p>
        </div>
      </section>
    </div>
  )
}
