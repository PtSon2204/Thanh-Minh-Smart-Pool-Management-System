import { useState } from 'react'
import { useQuery } from '@tanstack/react-query'
import apiClient from '../../services/apiClient'
import { Avatar, Skeleton, Modal } from 'antd'
import { QrcodeOutlined, UserOutlined } from '@ant-design/icons'
import { QRCodeSVG } from 'qrcode.react'
import dayjs from 'dayjs'
import './MyCardPage.css'
import { useAuthStore } from '../../features/auth/store/authStore'
import profileService from '../../features/profiles/services/profileService'

export default function MyCardPage() {
  const { session } = useAuthStore()
  const [selectedTicket, setSelectedTicket] = useState(null)
  
  const { data: profile } = useQuery({
    queryKey: ['myProfile'],
    queryFn: profileService.getMyProfile,
    enabled: !!session
  })

  const { data: tickets, isLoading } = useQuery({
    queryKey: ['myTickets'],
    queryFn: async () => {
      const res = await apiClient.get('/api/tickets/me')
      return res.data
    },
    enabled: !!session
  })

  return (
    <div className="mc-page">
      <div className="mc-header">
        <h2>Thẻ của tôi</h2>
      </div>

      <div className="mc-body">
        {/* Pass Card */}
        <div className="mc-sidebar">
        <div className="mc-smart-card">
          <div className="mc-card-top">
            <div className="mc-card-title">SMART POOL PASS</div>
            <div className="mc-card-type">Phổ thông</div>
          </div>
          
          <div className="mc-card-content">
            <div className="mc-avatar-wrapper">
              {profile?.avatarUrl ? (
                <img src={profile.avatarUrl} alt="Avatar" className="mc-avatar-img" />
              ) : (
                <Avatar size={80} icon={<UserOutlined />} className="mc-avatar-fallback" />
              )}
            </div>
            
            <div className="mc-card-info">
              <div className="mc-info-label">Họ và tên</div>
              <div className="mc-info-value">{profile?.fullName || session?.username || 'Đang tải...'}</div>
              
              <div className="mc-info-label" style={{ marginTop: '12px' }}>Tài khoản</div>
              <div className="mc-info-value">{session?.username || '---'}</div>
            </div>
          </div>

          <div className="mc-card-bottom">
            <div className="mc-badge">Điện tử</div>
            <div className="mc-expiry">
              <span className="mc-expiry-label">Ngày hết hạn</span>
              <span className="mc-expiry-value">Vô thời hạn</span>
            </div>
          </div>
        </div>
        </div>

        {/* Ticket List */}
        <div className="mc-main-content">
        <div className="mc-ticket-section">
          <div className="mc-section-header">
            <h3>DANH SÁCH VÉ</h3>
            <button className="mc-buy-btn" onClick={() => window.location.href = '/bang-gia'}>+ Mua vé</button>
          </div>

          <div className="mc-ticket-list">
            {isLoading ? (
              <Skeleton active />
            ) : tickets && tickets.length > 0 ? (
              tickets.map(ticket => (
                <div key={ticket.id} className="mc-ticket-item">

                  <div className="mc-ticket-details">
                    <div className="mc-ticket-name">
                      {ticket.ticketName}
                      <span className="mc-ticket-badge">{ticket.ticketCategory === 'VE_THANG' ? 'Vé tháng' : 'Vé lượt'}</span>
                    </div>
                    <div className="mc-ticket-code">Mã: {ticket.qrCode}</div>
                    <div className="mc-ticket-dates">
                      {ticket.ticketCategory === 'VE_THANG' 
                        ? `${dayjs(ticket.issueDate).format('DD/MM/YYYY')} ➔ ${dayjs(ticket.expiryDate).format('DD/MM/YYYY')}`
                        : `HSD: ${dayjs(ticket.expiryDate).format('DD/MM/YYYY')}`
                      }
                    </div>
                  </div>
                  <div className="mc-ticket-right">
                    <button 
                      className="mc-qr-btn" 
                      onClick={() => setSelectedTicket(ticket)}
                      title="Hiển thị mã QR"
                    >
                      <QrcodeOutlined />
                    </button>
                  </div>
                </div>
              ))
            ) : (
              <div className="mc-empty-tickets">
                Bạn chưa có vé nào. Hãy mua vé để sử dụng dịch vụ.
              </div>
            )}
          </div>
        </div>
        </div>
      </div>

      <Modal
        title="Mã QR Vé Của Bạn"
        open={!!selectedTicket}
        onCancel={() => setSelectedTicket(null)}
        footer={null}
        centered
        width={320}
      >
        <div style={{ textAlign: 'center', padding: '20px 0' }}>
          {selectedTicket && (
            <>
              <QRCodeSVG 
                value={selectedTicket.qrCode} 
                size={220}
                level="H"
                includeMargin
              />

              <div style={{ marginTop: '8px', color: '#666' }}>
                Đưa mã này cho nhân viên để quét khi qua cổng.
              </div>
            </>
          )}
        </div>
      </Modal>
    </div>
  )
}
