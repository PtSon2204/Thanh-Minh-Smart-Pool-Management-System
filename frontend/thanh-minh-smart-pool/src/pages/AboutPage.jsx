import { useEffect } from 'react';
import { Link } from 'react-router-dom';
import './AboutPage.css';
import imgBeBoi from '../assets/anh-be-boi.jpg';

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
      { threshold: 0.15 }
    );
    revealEls.forEach((el) => observer.observe(el));
    return () => observer.disconnect();
  }, []);

  return (
    <div className="about-page">
      {/* ── 1. HERO ── */}
      <section className="about-hero">
        <div className="container">
          <h1>Về Bể bơi Thành Minh</h1>
          <p className="hero-subtitle">
            Hơn 15 năm mang đến không gian bơi lội an toàn, chất lượng và thân thiện
            cho mọi gia đình tại Thành phố Hồ Chí Minh.
          </p>
          <div className="hero-stats">
            <div className="hero-stat-item">
              <span className="stat-number">15+</span>
              <span className="stat-label">Năm hoạt động</span>
            </div>
            <div className="hero-stat-item">
              <span className="stat-number">2</span>
              <span className="stat-label">Bể bơi tiêu chuẩn</span>
            </div>
            <div className="hero-stat-item">
              <span className="stat-number">AI</span>
              <span className="stat-label">Camera giám sát</span>
            </div>
          </div>
        </div>
      </section>

      {/* ── 2. STORY ── */}
      <section className="about-story">
        <div className="container">
          <div className="about-story-grid">
            {/* Left: ảnh bể bơi */}
            <div className="story-image reveal">
              <img src={imgBeBoi} alt="Ảnh bể bơi Thành Minh" className="pool-photo" />
            </div>
            {/* Right: text */}
            <div className="story-text reveal">
              <h2>Câu chuyện của chúng tôi</h2>
              <p>
                Bể bơi Thành Minh được thành lập năm 2010 với sứ mệnh mang đến
                một môi trường bơi lội hiện đại, an toàn và thân thiện cho người
                dân thành phố. Trải qua hơn 15 năm xây dựng và phát triển, chúng
                tôi đã không ngừng nâng cấp cơ sở vật chất, tích hợp công nghệ
                hiện đại để bảo vệ an toàn cho mọi khách hàng.
              </p>
              <p>
                Hệ thống của chúng tôi gồm <strong>2 bể bơi riêng biệt</strong>: một bể dành cho trẻ
                em cao dưới 1,4m và một bể lớn kích thước 10x23m dành cho người
                cao trên 1,4m. Toàn bộ khu vực đều được trang bị hệ thống <strong>Camera giám sát tích hợp AI</strong> nhằm phát hiện và cảnh báo nguy hiểm kịp thời.
              </p>
              <p>
                Ngoài ra, Thành Minh cung cấp đầy đủ các tiện ích như: dịch vụ trông xe chu đáo, tủ đồ miễn phí, cho thuê phao/kính bơi/khăn tắm, và khu vực đồ ăn nhanh (bim bim, nước, xúc xích...) giúp bạn có trải nghiệm trọn vẹn nhất.
              </p>
            </div>

            {/* Right: stat card */}
            <div className="stat-card reveal">
              <div className="stat-item">
                <span className="stat-value">2010</span>
                <span className="stat-label">Năm thành lập</span>
              </div>
              <div className="stat-item">
                <span className="stat-value">2</span>
                <span className="stat-label">Bể bơi hiện đại</span>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* ── 3. MISSION ── */}
      <section className="about-mission">
        <div className="container">
          <h2 className="section-title reveal">Tầm nhìn &amp; Sứ mệnh</h2>
          <div className="mission-grid">
            <div className="mission-card reveal">
              <div className="mission-icon">🎯</div>
              <h3>Tầm nhìn</h3>
              <p>
                Trở thành trung tâm bơi lội hàng đầu khu vực, cung cấp dịch vụ
                đạt chuẩn quốc tế, góp phần nâng cao sức khỏe cộng đồng và phát
                triển phong trào bơi lội tại Việt Nam.
              </p>
            </div>
            <div className="mission-card reveal">
              <div className="mission-icon">💧</div>
              <h3>Sứ mệnh</h3>
              <p>
                Mang đến môi trường bơi lội an toàn, sạch sẽ và chuyên nghiệp.
                Chúng tôi cam kết đào tạo kỹ năng bơi lội, bảo vệ sức khỏe và
                tạo ra những trải nghiệm tuyệt vời cho mọi lứa tuổi.
              </p>
            </div>
          </div>
        </div>
      </section>

      {/* ── 4. TEAM ── */}
      <section className="about-team">
        <div className="container">
          <h2 className="section-title reveal">Đội ngũ của chúng tôi</h2>
          <div className="team-grid">
            <div className="team-card reveal">
              <div className="team-avatar">👨‍💼</div>
              <h3>Nguyễn Văn An</h3>
              <p className="team-role">Giám đốc</p>
              <p className="team-desc">
                Hơn 20 năm kinh nghiệm trong ngành thể thao và quản lý cơ sở
                vật chất, dẫn dắt Thành Minh từ những ngày đầu thành lập.
              </p>
            </div>
            <div className="team-card reveal">
              <div className="team-avatar">👩‍💼</div>
              <h3>Trần Thị Bình</h3>
              <p className="team-role">Quản lý vận hành</p>
              <p className="team-desc">
                Chịu trách nhiệm điều phối toàn bộ hoạt động hàng ngày, đảm bảo
                chất lượng dịch vụ và sự hài lòng của khách hàng.
              </p>
            </div>
            <div className="team-card reveal">
              <div className="team-avatar">👨‍🔧</div>
              <h3>Lê Hoàng Cường</h3>
              <p className="team-role">Trưởng bộ phận kỹ thuật</p>
              <p className="team-desc">
                Phụ trách hệ thống lọc nước, vệ sinh bể bơi và bảo trì toàn bộ
                trang thiết bị theo tiêu chuẩn an toàn nghiêm ngặt.
              </p>
            </div>
          </div>
        </div>
      </section>

      {/* ── 5. ACHIEVEMENTS ── */}
      <section className="about-achievements">
        <div className="container">
          <h2 className="section-title reveal">Thành tích nổi bật</h2>
          <div className="achievements-grid">
            <div className="achievement-item reveal">
              <div className="achievement-icon">🏆</div>
              <h3>Top 10 Bể bơi tốt nhất TP.HCM</h3>
              <p>
                Được tạp chí Thể thao &amp; Sức khỏe bình chọn liên tục 5 năm
                liền (2019 – 2024).
              </p>
            </div>
            <div className="achievement-item reveal">
              <div className="achievement-icon">🥇</div>
              <h3>Chứng nhận An toàn Quốc gia</h3>
              <p>
                Đạt chứng nhận an toàn bơi lội cấp quốc gia do Bộ Văn hóa,
                Thể thao và Du lịch cấp năm 2022.
              </p>
            </div>
            <div className="achievement-item reveal">
              <div className="achievement-icon">🌟</div>
              <h3>Đơn vị Dịch vụ xuất sắc</h3>
              <p>
                Nhận giải thưởng "Đơn vị Dịch vụ Thể thao Xuất sắc" của Sở
                Văn hóa &amp; Thể thao TP.HCM năm 2023.
              </p>
            </div>
            <div className="achievement-item reveal">
              <div className="achievement-icon">💚</div>
              <h3>Cam kết Xanh - Sạch - Đẹp</h3>
              <p>
                Triển khai hệ thống lọc nước thân thiện môi trường, đạt tiêu
                chuẩn xanh của Liên đoàn Bơi lội Việt Nam.
              </p>
            </div>
          </div>
        </div>
      </section>

      {/* ── 6. CTA ── */}
      <section className="about-cta">
        <div className="container">
          <h2>Sẵn sàng trải nghiệm?</h2>
          <p>Đặt vé ngay hôm nay hoặc liên hệ chúng tôi để được tư vấn thêm.</p>
          <div className="cta-buttons">
            <Link to="/dat-ve" className="btn-cta btn-cta-primary">
              🎫 Đặt vé ngay
            </Link>
            <Link to="/lien-he" className="btn-cta btn-cta-outline">
              📞 Liên hệ chúng tôi
            </Link>
          </div>
        </div>
      </section>
    </div>
  );
}
