import { useState } from 'react'
import { Button, Row, Col, Spin, Empty, Typography } from 'antd'
import { PlusOutlined } from '@ant-design/icons'
import { useTicketTypes } from '../../features/tickets/hooks/useTicketTypes'
import TicketTypeCard from '../../features/tickets/components/TicketTypeCard'
import TicketTypeFormModal from '../../features/tickets/components/TicketTypeFormModal'

const { Title } = Typography

export default function TicketTypePage() {
  const [modalOpen, setModalOpen] = useState(false)
  const { data: ticketTypes, isLoading, isError } = useTicketTypes()

  return (
    <div>
      {/* Page Header */}
      <div
        style={{
          background: 'linear-gradient(90deg, #005f8e 0%, #00b4d8 100%)',
          padding: '20px 24px',
          borderRadius: 12,
          color: '#fff',
          marginBottom: 24,
          display: 'flex',
          justifyContent: 'space-between',
          alignItems: 'center',
          boxShadow: '0 4px 12px rgba(0, 119, 182, 0.15)',
        }}
      >
        <div>
          <Title level={4} style={{ color: '#fff', margin: 0 }}>
            🎫 Quản Lý Loại Vé
          </Title>
          <p style={{ color: 'rgba(255,255,255,0.85)', margin: '4px 0 0' }}>
            Danh sách các loại vé đang cung cấp tại hồ bơi
          </p>
        </div>
        <Button
          type="primary"
          icon={<PlusOutlined />}
          size="large"
          onClick={() => setModalOpen(true)}
          style={{
            background: '#fff',
            color: '#005f8e',
            fontWeight: 600,
            border: 'none',
          }}
        >
          Thêm loại vé
        </Button>
      </div>

      {/* Card Grid */}
      {isLoading && (
        <div style={{ textAlign: 'center', padding: 64 }}>
          <Spin size="large" tip="Đang tải danh sách loại vé..." />
        </div>
      )}

      {isError && (
        <Empty
          description="Không thể tải danh sách loại vé. Kiểm tra kết nối server."
          style={{ padding: 48 }}
        />
      )}

      {!isLoading && !isError && ticketTypes?.length === 0 && (
        <Empty
          description="Chưa có loại vé nào. Nhấn 'Thêm loại vé' để bắt đầu."
          style={{ padding: 48 }}
        />
      )}

      {!isLoading && !isError && ticketTypes?.length > 0 && (
        <Row gutter={[24, 24]}>
          {ticketTypes.map((tt) => (
            <Col key={tt.id} xs={24} sm={12} lg={8} xl={6}>
              <TicketTypeCard ticketType={tt} />
            </Col>
          ))}
        </Row>
      )}

      {/* Modal thêm loại vé */}
      <TicketTypeFormModal
        open={modalOpen}
        onClose={() => setModalOpen(false)}
      />
    </div>
  )
}
