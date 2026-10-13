import { useState } from 'react'
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query'
import apiClient from '../../services/apiClient'
import { Avatar, Skeleton, Modal, Input, message } from 'antd'
import { QrcodeOutlined, UserOutlined, EditOutlined, CheckOutlined, CloseOutlined } from '@ant-design/icons'
import { QRCodeSVG } from 'qrcode.react'
import dayjs from 'dayjs'
import './MyCardPage.css'
import { useAuthStore } from '../../features/auth/store/authStore'
import profileService from '../../features/profiles/services/profileService'
import ticketService from '../../features/tickets/services/ticketService'

export default function MyCardPage() {
  const { session } = useAuthStore()
  const queryClient = useQueryClient()
  const [selectedTicket, setSelectedTicket] = useState(null)
  // State cho chỉnh sửa mã: ticketId đang được edit
  const [editingId, setEditingId] = useState(null)
  const [editValue, setEditValue] = useState('')

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

  // Mutation cập nhật mã vé
  const updateCodeMutation = useMutation({
    mutationFn: ({ ticketId, code }) => ticketService.updateCode(ticketId, code),
    onSuccess: (data) => {
      message.success(data.message || 'Cập nhật mã thành công!')
      queryClient.invalidateQueries({ queryKey: ['myTickets'] })
      setEditingId(null)
      setEditValue('')
    },
    onError: (err) => {
      const msg = err?.response?.data?.message || 'Cập nhật thất bại. Vui lòng thử lại.'
      message.error(msg)
    }
  })

  const handleStartEdit = (ticket) => {
    setEditingId(ticket.id)
    setEditValue(ticket.qrCode)
  }

  const handleCancelEdit = () => {
    setEditingId(null)
    setEditValue('')
  }

  const handleSaveCode = (ticketId) => {
    if (!/^\d{6}$/.test(editValue)) {
      message.warning('Mã vé phải là đúng 6 chữ số (0–9).')
      return
    }
    updateCodeMutation.mutate({ ticketId, code: editValue })
  }

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

                    {/* Mã vé — hiển thị hoặc chỉnh sửa */}
                    {editingId === ticket.id ? (
                      <div className="mc-code-edit-row">
                        <Input
                          value={editValue}
                          onChange={(e) => {
                            // Chỉ nhận số, tối đa 6 ký tự
                            const val = e.target.value.replace(/\D/g, '').slice(0, 6)
                            setEditValue(val)
                          }}
                          maxLength={6}
                          placeholder="6 chữ số"
                          size="small"
                          style={{ width: 100, fontFamily: 'monospace', letterSpacing: 2 }}
                          autoFocus
                        />
                        <button
                          className="mc-code-action-btn mc-code-save-btn"
                          onClick={() => handleSaveCode(ticket.id)}
                          disabled={updateCodeMutation.isPending}
                          title="Lưu mã"
                        >
                          <CheckOutlined />
                        </button>
                        <button
                          className="mc-code-action-btn mc-code-cancel-btn"
                          onClick={handleCancelEdit}
                          disabled={updateCodeMutation.isPending}
                          title="Hủy"
                        >
                          <CloseOutlined />
                        </button>
                      </div>
                    ) : (
                      <div className="mc-ticket-code-row">
                        <span className="mc-ticket-code">Mã: {ticket.qrCode}</span>
                        {ticket.status === 'ACTIVE' && (
                          <button
                            className="mc-code-edit-icon"
                            onClick={() => handleStartEdit(ticket)}
                            title="Đổi mã vé"
                          >
                            <EditOutlined />
                          </button>
                        )}
                      </div>
                    )}

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
              <div style={{ marginTop: '12px', fontSize: 28, fontWeight: 700, letterSpacing: 6, fontFamily: 'monospace', color: '#1a7a4a' }}>
                {selectedTicket.qrCode}
              </div>
              <div style={{ marginTop: '6px', color: '#666', fontSize: 13 }}>
                Đưa mã này cho nhân viên để quét khi qua cổng.
              </div>
            </>
          )}
        </div>
      </Modal>
    </div>
  )
}
