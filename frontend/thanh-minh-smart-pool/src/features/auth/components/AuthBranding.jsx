import { Link } from 'react-router-dom'

export default function AuthBranding() {
  return (
    <>
      <div className="auth-pane-logo">
        <div className="auth-pane-logo-inner">
          <div className="auth-logo-icon">
            <svg viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2.2" strokeLinecap="round" strokeLinejoin="round" width="20" height="20">
              <path d="M12 2.69l5.66 5.66a8 8 0 1 1-11.31 0z" />
            </svg>
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
