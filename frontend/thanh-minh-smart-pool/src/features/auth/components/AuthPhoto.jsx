export default function AuthPhoto() {
  return (
    <section className="auth-photo" aria-label="Hình ảnh bể bơi Thành Minh">
      <video className="auth-photo-img" autoPlay muted loop playsInline preload="auto">
        <source src="https://d8j0ntlcm91z4.cloudfront.net/user_38xzZboKViGWJOttwIXH07lWA1P/hf_20260424_064411_9e9d7f84-9277-41f4-ab10-59172d89e6be.mp4" type="video/mp4" />
      </video>
      <div className="auth-photo-overlay" aria-hidden="true" />
      <div className="auth-bubbles" aria-hidden="true">
        {[...Array(6)].map((_, index) => <div key={index} className={`auth-bubble ab${index + 1}`} />)}
      </div>
      <div className="auth-waves-wrap" aria-hidden="true">
        <div className="auth-wave aw1">
          <svg viewBox="0 0 2880 120" preserveAspectRatio="none" xmlns="http://www.w3.org/2000/svg">
            <path d="M0,60 C480,120 960,0 1440,60 C1920,120 2400,0 2880,60 L2880,120 L0,120 Z" fill="rgba(0,119,182,0.35)" />
          </svg>
        </div>
        <div className="auth-wave aw2">
          <svg viewBox="0 0 2880 120" preserveAspectRatio="none" xmlns="http://www.w3.org/2000/svg">
            <path d="M0,80 C480,20 960,100 1440,50 C1920,0 2400,90 2880,40 L2880,120 L0,120 Z" fill="rgba(0,180,216,0.25)" />
          </svg>
        </div>
      </div>
      <div className="auth-photo-content">
        <div className="auth-photo-badge">
          <svg viewBox="0 0 24 24" fill="none" stroke="white" strokeWidth="2" width="16" height="16"><path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z" /></svg>
          An toàn · Tiêu chuẩn · Chất lượng
        </div>
        <h2 className="auth-photo-headline">Trải nghiệm bơi lội<br /><span>đẳng cấp</span> tại<br />Thành Minh</h2>
        <p className="auth-photo-sub">Bể bơi hiện đại với camera AI giám sát an toàn 24/7, dịch vụ cho thuê phụ kiện và đồ ăn nhanh ngay tại chỗ.</p>
      </div>
    </section>
  )
}
