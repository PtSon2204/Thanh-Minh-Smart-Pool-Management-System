import { Link } from 'react-router-dom'

export default function AuthBranding() {
  return (
    <>
      <div className="auth-pane-logo">
        <div className="auth-pane-logo-inner">
          <div className="auth-logo-icon">
            <svg viewBox="0 0 24 24" fill="white"><path d="M12 2C8.13 2 5 5.13 5 9c0 5.25 7 13 7 13s7-7.75 7-13c0-3.87-3.13-7-7-7zm0 9.5c-1.38 0-2.5-1.12-2.5-2.5s1.12-2.5 2.5-2.5 2.5 1.12 2.5 2.5-1.12 2.5-2.5 2.5z" /></svg>
          </div>
          <div>
            <div className="auth-logo-name">Thành Minh</div>
            <div className="auth-logo-sub">Bể bơi thông minh</div>
          </div>
        </div>
        <Link to="/" className="auth-back-btn">
          <svg viewBox="0 0 20 20" fill="none" stroke="currentColor" strokeWidth="2.2" width="14" height="14">
            <path d="M12 4l-6 6 6 6" strokeLinecap="round" strokeLinejoin="round" />
          </svg>
          Trang chủ
        </Link>
      </div>
      <div className="auth-form-wave" aria-hidden="true">
        <svg className="fw1" viewBox="0 0 1440 120" preserveAspectRatio="none">
          <path d="M0,60 C360,120 1080,0 1440,60 L1440,120 L0,120 Z" fill="rgba(0,119,182,0.06)" />
        </svg>
        <svg className="fw2" viewBox="0 0 1440 120" preserveAspectRatio="none">
          <path d="M0,80 C480,20 960,120 1440,40 L1440,120 L0,120 Z" fill="rgba(0,180,216,0.05)" />
        </svg>
      </div>
    </>
  )
}
