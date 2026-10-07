import './PricingPage.css'
import { useNavigate, useLocation } from 'react-router-dom'
import { useQuery } from '@tanstack/react-query'
import axios from 'axios'
import { useState } from 'react'
import { message } from 'antd'
import { useAuthStore } from '../../features/auth/store/authStore'
import { useCartStore } from '../../features/cart/store/cartStore'
import profileService from '../../features/profiles/services/profileService'


const categoryMeta = {
  VE_THANG: { label: 'VÉ THÁNG', color: '#005f8e', bg: '#e8f4fa' },
  VE_LUOT:  { label: 'VÉ LƯỢT',  color: '#d46b08', bg: '#fff7e6' },
}

const getInfoGrid = (ticket) => {
  const isThang = ticket.ticketCategory === 'VE_THANG'
  return [
    { icon: '📍', label: 'Địa điểm',   value: 'Bể bơi Thanh Minh' },
    { icon: '⏱',  label: 'Hiệu lực',   value: isThang ? `${ticket.durationDays || 30} ngày` : 'Sử dụng 1 lần' },
    { icon: '👤',  label: 'Đối tượng',  value: isThang ? 'Khách đăng ký tháng' : 'Mọi khách hàng' },
    { icon: '🕒',  label: 'Giờ mở cửa', value: '5:30 - 21:00 hàng ngày' },
  ]
}

export default function PricingPage() {
  const [search, setSearch]     = useState('')
  const [activeTab, setActiveTab] = useState('ALL')

  const navigate = useNavigate()
  const location = useLocation()
  const { session } = useAuthStore()
  const addToCart = useCartStore(state => state.addToCart)

  const { data: apiData, isLoading, isError } = useQuery({
    queryKey: ['ticket-types-public'],
    queryFn: async () => {
      const res = await axios.get('/api/ticket-types?isActive=true&pageIndex=1&pageSize=100')
      return res.data.items || res.data
    },
    retry: 1,
  })

  const checkProfileAndExecute = async (action) => {
    if (!session) {
      navigate('/dang-nhap?redirect=' + encodeURIComponent(location.pathname))
      return
    }

    try {
      const profile = await profileService.getMyProfile()
      if (!profile.address || !profile.dateOfBirth) {
        message.warning('Vui lòng cập nhật đầy đủ thông tin cá nhân trước khi mua vé.')
        navigate('/ho-so')
      } else {
        action()
      }
    } catch (error) {
      console.error(error)
      message.error('Lỗi kiểm tra thông tin, vui lòng cập nhật hồ sơ.')
      navigate('/ho-so')
    }
  }

  const handleAddToCart = (ticket) => {
    checkProfileAndExecute(() => {
      addToCart({
        id: ticket.id,
        name: ticket.name,
        price: ticket.price,
        
      })
    })
  }

  const tickets = apiData && apiData.length > 0 ? apiData : []

  const filtered = tickets.filter(t => {
    const matchTab = activeTab === 'ALL' || t.ticketCategory === activeTab
    const matchSearch = t.name.toLowerCase().includes(search.toLowerCase())
    return matchTab && matchSearch
  })

  return (
    <div className="pl-page">
      <section className="pl-hero">
        <h1>Bảng Giá Vé</h1>
        <p>Minh bạch • Rõ ràng • Phù hợp mọi đối tượng</p>
      </section>

      <div className="pl-body">
        <div className="pl-toolbar">
          <div className="pl-search">
            <span className="pl-search-icon">🔍</span>
            <input
              placeholder="Tìm tên vé, loại vé..."
              value={search}
              onChange={e => setSearch(e.target.value)}
            />
          </div>
        </div>

        <div className="pl-filters">
          <span className="pl-filter-label">LOẠI VÉ:</span>
          {[
            { key: 'ALL',      label: 'Tất cả' },
            { key: 'VE_LUOT',  label: 'Vé lượt' },
            { key: 'VE_THANG', label: 'Vé tháng' },
          ].map(f => (
            <button
              key={f.key}
              className={`pl-chip ${activeTab === f.key ? ' active' : ''}`}
              onClick={() => setActiveTab(f.key)}
            >
              {f.label}
            </button>
          ))}
        </div>

        {isLoading && (
          <div className="pl-list">
            {[1, 2, 3].map(i => <div key={i} className="pl-skeleton" />)}
          </div>
        )}

        {isError && !isLoading && (
          <div className="pl-error">
            ⚠️ Không thể tải dữ liệu từ máy chủ. Vui lòng thử lại sau.
          </div>
        )}

        {!isLoading && !isError && filtered.length === 0 && (
          <div className="pl-empty">Không tìm thấy loại vé phù hợp.</div>
        )}

        {!isLoading && !isError && (
          <div className="pl-list">
            {filtered.map((ticket, idx) => {
              const meta = categoryMeta[ticket.ticketCategory] ?? categoryMeta.VE_LUOT

              const info = getInfoGrid(ticket)
              const isPopular = idx === 0

              return (
                <div className="pl-card" key={ticket.id}>
                  <div className="pl-card-inner">
                    

                    <div className="pl-card-body">
                      <div className="pl-card-tags">
                        <span
                          className="pl-tag"
                          style={{ color: meta.color, background: meta.bg, border: `1px solid ${meta.color}` }}
                        >
                          {meta.label}
                        </span>
                        {isPopular && (
                          <span className="pl-tag pl-tag-hot">🔥 BÁN CHẠY NHẤT</span>
                        )}
                      </div>

                      <h2 className="pl-card-title">{ticket.name}</h2>

                      <p className="pl-card-desc">
                        {ticket.ticketCategory === 'VE_THANG'
                          ? `Vé tháng ${ticket.durationDays || 30} ngày - Vào bể bơi 1 lần mỗi ngày, có mã QR riêng liên kết tài khoản khách hàng.`
                          : 'Vé lượt tại quầy - Sử dụng ngay trong ngày, phù hợp khách vãng lai.'}
                      </p>

                      <div className="pl-info-grid">
                        {info.map((item, i) => (
                          <div className="pl-info-cell" key={i}>
                            <span className="pl-info-icon">{item.icon}</span>
                            <span className="pl-info-label">{item.label}:</span>
                            <span className="pl-info-value">{item.value}</span>
                          </div>
                        ))}
                      </div>

                      <div className="pl-card-footer">
                        <div className="pl-price">
                          <span className="pl-price-current">
                            {new Intl.NumberFormat('vi-VN').format(ticket.price)}đ
                          </span>
                        </div>
                        <div className="pl-cta-group">
                          <button className="pl-cta-btn pl-cta-cart" onClick={() => handleAddToCart(ticket)}>
                            🛒 Thêm giỏ hàng
                          </button>
                        </div>
                      </div>
                    </div>
                  </div>
                </div>
              )
            })}
          </div>
        )}
      </div>
    </div>
  )
}
