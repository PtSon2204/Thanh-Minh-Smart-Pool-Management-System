import { useEffect } from 'react';
import { Link } from 'react-router-dom';
import './AboutPage.css';
import imgBeBoi from '../../assets/anh-be-boi.jpg';
import AmbientHeroBg from '../../components/shared/AmbientHeroBg';

export default function AboutPage() {
  useEffect(() => {
    const revealEls = document.querySelectorAll('.reveal');
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add('active');
          }
        });
      },
      { threshold: 0.12 }
    );
    revealEls.forEach((el) => observer.observe(el));
    return () => observer.disconnect();
  }, []);

  return (
    <div className="about-page">
      {/* ── 1. HERO ── */}
      <section className="about-hero">
        <AmbientHeroBg />

        {/* Animated wave smoothly connecting to #f8fafc */}
        <div className="about-hero-waves" aria-hidden="true">
          <div className="hero-wave-track hw1">
            <svg viewBox="0 0 1440 110" preserveAspectRatio="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M0,55 C240,110 480,0 720,55 C960,110 1200,0 1440,55 L1440,110 L0,110 Z" fill="rgba(248, 250, 252, 0.4)"/>
            </svg>
            <svg viewBox="0 0 1440 110" preserveAspectRatio="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M0,55 C240,110 480,0 720,55 C960,110 1200,0 1440,55 L1440,110 L0,110 Z" fill="rgba(248, 250, 252, 0.4)"/>
            </svg>
          </div>
          <div className="hero-wave-track hw2">
            <svg viewBox="0 0 1440 110" preserveAspectRatio="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M0,75 C360,20 720,110 1080,55 C1260,28 1380,80 1440,65 L1440,110 L0,110 Z" fill="#f8fafc"/>
            </svg>
            <svg viewBox="0 0 1440 110" preserveAspectRatio="none" xmlns="http://www.w3.org/2000/svg">
              <path d="M0,75 C360,20 720,110 1080,55 C1260,28 1380,80 1440,65 L1440,110 L0,110 Z" fill="#f8fafc"/>
            </svg>
          </div>
        </div>

        {/* Content */}
        <div className="container about-hero-content">
          <div className="about-hero-badge">
            <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" width="14" height="14">
              <path d="M12 22s8-4 8-10V5l-8-3-8 3v7c0 6 8 10 8 10z"/>
            </svg>
            Hơn 10 năm đồng hành cùng sức khỏe cộng đồng
          </div>
          <h1>Về Bể bơi Thành Minh</h1>
          <p className="hero-subtitle">
            Hơn một thập kỷ kiến tạo không gian bơi lội trong lành, an toàn và chuyên nghiệp hàng đầu tại Đông Anh, Hà Nội.
          </p>

          <div className="hero-stats">
            <div className="hero-stat-item">
              <span className="stat-number">10+</span>
              <span className="stat-label">Năm phục vụ</span>
            </div>
            <div className="hero-stat-item">
              <span className="stat-number">02</span>
              <span className="stat-label">Bể bơi tiêu chuẩn</span>
            </div>
            <div className="hero-stat-item">
              <span className="stat-number">AI 24/7</span>
              <span className="stat-label">Camera an toàn</span>
            </div>
          </div>
        </div>
      </section>

      {/* ── 2. STORY ── */}
      <section className="about-story">
        <div className="container">
          <div className="about-story-grid">
            {/* Left: Pool Photo Frame */}
            <div className="story-image-wrapper reveal">
              <div className="story-image-card">
                <img src={imgBeBoi} alt="Bể bơi Thành Minh" className="pool-photo" loading="lazy" />
                <div className="story-image-badge">
                  <span>📍 Đông Anh, Hà Nội</span>
                </div>
              </div>
            </div>

            {/* Right: Text Story */}
            <div className="story-text reveal">
              <span className="section-kicker">✦ CÂU CHUYỆN KHỞI NGUỒN</span>
              <h2>Hành trình kiến tạo không gian bơi lội an toàn</h2>
              <p>
                Bể bơi Thành Minh được thành lập năm 2016 với sứ mệnh mang đến
                một môi trường bơi lội hiện đại, an toàn và thân thiện cho người
                dân thành phố. Trải qua hơn 10 năm xây dựng và phát triển, chúng
                tôi đã không ngừng nâng cấp cơ sở vật chất, tích hợp công nghệ
                tiên tiến để bảo vệ an toàn cho mọi khách hàng.
              </p>
              
              <div className="story-highlights-grid">
                <div className="story-highlight-card">
                  <div className="sh-info">
                    <h4>2 Bể bơi riêng biệt</h4>
                    <p>Bể nông an toàn cho trẻ em &lt;1,4m và bể lớn 10x23m cho người lớn.</p>
                  </div>
                </div>

                <div className="story-highlight-card">
                  <div className="sh-info">
                    <h4>Camera AI giám sát 24/7</h4>
                    <p>Công nghệ trí tuệ nhân tạo phát hiện sớm nguy cơ mất an toàn dưới nước.</p>
                  </div>
                </div>

                <div className="story-highlight-card">
                  <div className="sh-info">
                    <h4>Tiện ích trọn gói</h4>
                    <p>Tủ đồ cá nhân miễn phí, trông giữ xe an ninh và dịch vụ cho thuê phụ kiện sạch.</p>
                  </div>
                </div>
              </div>
            </div>            
          </div>
        </div>
      </section>

      {/* ── 3. VISION & MISSION ── */}
      <section className="about-mission">
        <div className="container">
          <div className="section-header reveal" style={{ textAlign: 'center' }}>
            <span className="section-kicker">✦ GIÁ TRỊ CỐT LÕI</span>
            <h2 className="section-title">Tầm nhìn &amp; Sứ mệnh</h2>
            <p className="section-subtitle">Định hướng phát triển bền vững vì sức khỏe và sự an tâm của cộng đồng</p>
          </div>

          <div className="mission-grid">
            <div className="mission-card vision-card reveal">
              <span className="mission-badge">Mục tiêu dài hạn</span>
              <h3>Tầm nhìn phát triển</h3>
              <p>
                Trở thành hệ thống hồ bơi thông minh tiêu chuẩn hàng đầu khu vực, ứng dụng công nghệ hiện đại vào quản lý vận hành nhằm tạo ra sân chơi thể thao dưới nước lành mạnh, bền vững và nâng tầm sức khỏe cộng đồng.
              </p>
            </div>

            <div className="mission-card mission-subcard reveal">
              <span className="mission-badge">Trách nhiệm hàng đầu</span>
              <h3>Sứ mệnh cam kết</h3>
              <p>
                Mang đến môi trường bơi lội an toàn, làn nước trong sạch chuẩn y tế và dịch vụ chăm sóc tận tâm. Chúng tôi bảo vệ an toàn tối đa cho từng lượt khách và lan tỏa niềm đam mê bơi lội đến mọi lứa tuổi.
              </p>
            </div>
          </div>
        </div>
      </section>

      {/* ── 4. CTA ── */}
      <section className="about-cta">
        <div className="container">
          <div className="about-cta-box reveal">
            <h2>Sẵn sàng cùng chúng tôi trải nghiệm?</h2>
            <p>Khám phá các gói vé bơi ưu đãi hoặc liên hệ để được hỗ trợ tư vấn chi tiết nhất.</p>
            <div className="cta-buttons">
              <Link to="/bang-gia" className="btn-primary cta-btn-white">
                <span>Xem bảng giá vé</span>
                <svg width="18" height="18" viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2.5" strokeLinecap="round" strokeLinejoin="round">
                  <line x1="5" y1="12" x2="19" y2="12" />
                  <polyline points="12 5 19 12 12 19" />
                </svg>
              </Link>
              <Link to="/lien-he" className="btn-outline">
                Liên hệ chúng tôi
              </Link>
            </div>
          </div>
        </div>
      </section>
    </div>
  );
}
