import './PricingPage.css'
import { Link } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import axios from 'axios'
import { useState, useEffect, useRef } from 'react'

const fallbackTickets = [
  {
    id: 1,
    name: 'Vé Trẻ em',
    description: 'Dành cho khách cao dưới 1,4m',
    price: 30000,
    applicableFor: 'Khách cao dưới 1,4m',
    features: ['Bể an toàn dành riêng cho trẻ', 'Camera AI giám sát an toàn 24/7', 'Sử dụng tủ đồ cá nhân miễn phí', 'Giá tiền minh bạch rõ ràng'],
  },
  {
    id: 2,
    name: 'Vé Người lớn',
    description: 'Dành cho khách cao từ 1,4m',
    price: 50000,
    applicableFor: 'Khách cao từ 1,4m',
    features: [
      'Bể lớn kích thước chuẩn 10x23m',
      'Camera AI giám sát an toàn 24/7',
      'Sử dụng tủ đồ cá nhân miễn phí',
      'Giá tiền minh bạch rõ ràng',
    ],
  },
  {
    id: 3,
    name: 'Thẻ tháng',
    description: 'Bơi thoả thích không giới hạn',
    price: 800000,
    applicableFor: 'Mọi đối tượng khách hàng',
    features: [
      'Tiết kiệm chi phí tối đa',
      'Được sử dụng toàn bộ 2 bể bơi',
      'Camera AI giám sát an toàn 24/7',
      'Sử dụng tủ đồ cá nhân miễn phí',
    ],
  },
]

const ticketIcons = ['👶', '🏊', '👨‍👩‍👧‍👦']

const pricingPolicies = [
  {
    icon: '🔄',
    title: 'Hoàn vé linh hoạt',
    desc: 'Hoàn 100% nếu hủy trước 24 giờ. Hoàn 50% nếu hủy trong vòng 24 giờ trước giờ vào.',
  },
  {
    icon: '🌧️',
    title: 'Chính sách thời tiết',
    desc: 'Miễn phí đổi vé hoặc hoàn tiền 100% nếu hồ bơi đóng cửa do thời tiết xấu.',
  },
  {
    icon: '📋',
    title: 'Điều khoản sử dụng',
    desc: 'Vé chỉ có giá trị trong ngày mua. Không áp dụng đồng thời nhiều chương trình ưu đãi.',
  },
]

const faqs = [
  {
    q: 'Tôi có thể mua vé trực tiếp tại quầy không?',
    a: 'Có, bạn có thể mua vé trực tiếp tại quầy thu ngân của bể bơi Thành Minh. Chúng tôi chấp nhận cả tiền mặt và thẻ ngân hàng.',
  },
  {
    q: 'Vé cuối tuần và vé ngày lễ có khác nhau không?',
    a: 'Có. Giá vé cuối tuần (Thứ 7, Chủ nhật) tăng thêm 20% và giá vé ngày lễ tăng thêm 30% so với ngày thường.',
  },
  {
    q: 'Trẻ em dưới bao nhiêu tuổi được miễn phí?',
    a: 'Trẻ em dưới 3 tuổi được miễn phí hoàn toàn khi đi cùng người lớn có vé hợp lệ.',
  },
  {
    q: 'Vé gia đình có thể áp dụng cho bao nhiêu người?',
    a: 'Vé gia đình áp dụng cho 2 người lớn và tối đa 2 trẻ em dưới 10 tuổi. Nếu có thêm thành viên, vui lòng mua thêm vé lẻ.',
  },
  {
    q: 'Tôi quên vé điện tử thì phải làm sao?',
    a: 'Bạn có thể cung cấp mã đặt vé hoặc số điện thoại đăng ký để nhân viên tra cứu và xác nhận tại cổng vào.',
  },
]

const getPriceByTab = (price, tab) => {
  if (tab === 'weekend') return Math.round(price * 1.2)
  if (tab === 'holiday') return Math.round(price * 1.3)
  return price
}

export default function PricingPage() {
  const [activeTab, setActiveTab] = useState('weekday')
  const revealRefs = useRef([])

  const { data: apiData, isLoading, isError } = useQuery({
    queryKey: ['ticket-types'],
    queryFn: async () => {
      const res = await axios.get('/api/v1/ticket-types')
      return res.data
    },
    retry: 1,
  })

  const tickets = apiData && apiData.length > 0 ? apiData : fallbackTickets

  // Scroll reveal
  useEffect(() => {
    const observer = new IntersectionObserver(
      (entries) => {
        entries.forEach((entry) => {
          if (entry.isIntersecting) {
            entry.target.classList.add('active')
          }
        })
      },
      { threshold: 0.1 }
    )
    revealRefs.current.forEach((el) => {
      if (el) observer.observe(el)
    })
    return () => observer.disconnect()
  }, [tickets])

  const addRevealRef = (el) => {
    if (el && !revealRefs.current.includes(el)) {
      revealRefs.current.push(el)
    }
  }

  return (
    <div className="pricing-page">
      {/* Hero */}
      <section className="pricing-hero">
        <div className="container">
          <h1>Bảng giá vé</h1>
          <p>Minh bạch • Rõ ràng • Phù hợp mọi đối tượng</p>
        </div>
      </section>

      {/* Tabs */}
      <section className="pricing-section">
        <div className="container">
          <div className="pricing-tabs">
            <button
              className={`tab-btn${activeTab === 'weekday' ? ' active' : ''}`}
              onClick={() => setActiveTab('weekday')}
            >
              Ngày thường
            </button>
            <button
              className={`tab-btn${activeTab === 'weekend' ? ' active' : ''}`}
              onClick={() => setActiveTab('weekend')}
            >
              Cuối tuần +20%
            </button>
            <button
              className={`tab-btn${activeTab === 'holiday' ? ' active' : ''}`}
              onClick={() => setActiveTab('holiday')}
            >
              Ngày lễ +30%
            </button>
          </div>

          {/* Loading skeleton */}
          {isLoading && (
            <div className="pricing-grid">
              <div className="skeleton-card" />
              <div className="skeleton-card" />
              <div className="skeleton-card" />
            </div>
          )}

          {/* Error */}
          {isError && !isLoading && (
            <div className="error-msg">
              ⚠️ Không thể tải dữ liệu vé từ máy chủ. Đang hiển thị dữ liệu mặc định.
            </div>
          )}

          {/* Pricing cards */}
          {!isLoading && (
            <div className="pricing-grid">
              {tickets.map((ticket, index) => (
                <div
                  key={ticket.id}
                  className={`pricing-card${index === 1 ? ' featured' : ''} reveal`}
                  ref={addRevealRef}
                >
                  {index === 1 && (
                    <span className="pricing-badge">Phổ biến nhất</span>
                  )}
                  <div className="card-icon">{ticketIcons[index] || '🎫'}</div>
                  <h3>{ticket.name}</h3>
                  <p className="card-desc">{ticket.description}</p>
                  <div className="price">
                    {getPriceByTab(ticket.price, activeTab).toLocaleString('vi-VN')}đ
                  </div>
                  <p className="applicable-for">
                    <strong>Áp dụng:</strong> {ticket.applicableFor}
                  </p>
                  <ul className="features">
                    {ticket.features.map((f, i) => (
                      <li key={i}>{f}</li>
                    ))}
                  </ul>
                  <Link to="/dat-ve" className="pricing-btn">
                    Đặt vé ngay
                  </Link>
                </div>
              ))}
            </div>
          )}
        </div>
      </section>

      {/* Policy */}
      <section className="pricing-policy">
        <div className="container">
          <h2 className="section-title">Chính sách vé</h2>
          <div className="policy-grid">
            {pricingPolicies.map((policy, index) => (
              <div
                key={index}
                className="policy-card reveal"
                ref={addRevealRef}
              >
                <div className="policy-icon">{policy.icon}</div>
                <h3>{policy.title}</h3>
                <p>{policy.desc}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* FAQ */}
      <section className="pricing-faq">
        <div className="container">
          <h2 className="section-title">Câu hỏi thường gặp</h2>
          <div className="faq-list">
            {faqs.map((faq, index) => (
              <details key={index} className="reveal" ref={addRevealRef}>
                <summary>{faq.q}</summary>
                <p className="faq-answer">{faq.a}</p>
              </details>
            ))}
          </div>
        </div>
      </section>
    </div>
  )
}
