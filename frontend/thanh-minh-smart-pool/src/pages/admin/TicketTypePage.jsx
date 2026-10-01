import { useState } from 'react'
import { Button, Row, Col, Spin, Empty, Typography, Modal, Input, Select, Pagination } from 'antd'
import { PlusOutlined, ExclamationCircleOutlined, SearchOutlined } from '@ant-design/icons'
import { useTicketTypes, useDeleteTicketType } from '../../features/tickets/hooks/useTicketTypes'
import { useToggleLockTicketType } from '../../features/tickets/hooks/useTicketTypeMutations'
import TicketTypeCard from '../../features/tickets/components/TicketTypeCard'
import TicketTypeFormModal from '../../features/tickets/components/TicketTypeFormModal'
import TicketTypeDetailModal from '../../features/tickets/components/TicketTypeDetailModal'
import { TicketCategoryOptions } from '../../features/tickets/types/ticketType'

const { Title } = Typography
const { confirm } = Modal

export default function TicketTypePage() {
  const [formModalOpen, setFormModalOpen] = useState(false)
  const [detailModalOpen, setDetailModalOpen] = useState(false)
  const [selectedTicket, setSelectedTicket] = useState(null)

  // Filters & Pagination State
  const [searchTerm, setSearchTerm] = useState('')
  const [category, setCategory] = useState(null)
  const [isActive, setIsActive] = useState(null)
  const [pageIndex, setPageIndex] = useState(1)
  const [pageSize, setPageSize] = useState(8)

  const { data: pagedData, isLoading, isError } = useTicketTypes({
    searchTerm,
    category,
    isActive,
    pageIndex,
    pageSize,
  })

  const ticketTypes = pagedData?.items || []
  const totalCount = pagedData?.totalCount || 0

  const { mutate: toggleLock } = useToggleLockTicketType()
  const { mutate: deleteTicketType } = useDeleteTicketType()

  const handleCreate = () => {
    setSelectedTicket(null)
    setFormModalOpen(true)
  }

  const handleEdit = (ticket) => {
    setSelectedTicket(ticket)
    setFormModalOpen(true)
  }

  const handleView = (ticket) => {
    setSelectedTicket(ticket)
    setDetailModalOpen(true)
  }

  const handleToggleLock = (ticket) => {
    confirm({
      title: ticket.isActive ? 'Khóa loại vé?' : 'Mở khóa loại vé?',
      icon: <ExclamationCircleOutlined />,
      content: `Bạn có chắc muốn ${ticket.isActive ? 'khóa' : 'mở khóa'} loại vé "${ticket.name}"?`,
      okText: 'Đồng ý',
      cancelText: 'Hủy',
      onOk() {
        toggleLock(ticket.id)
      },
    })
  }

  const handleDelete = (ticket) => {
    confirm({
      title: 'Xóa loại vé?',
      icon: <ExclamationCircleOutlined style={{ color: '#ff4d4f' }} />,
      content: `Bạn có chắc muốn xóa loại vé "${ticket.name}"? Thao tác này không thể hoàn tác nhưng các vé đã bán ra sẽ không bị ảnh hưởng.`,
      okText: 'Xóa',
      okType: 'danger',
      cancelText: 'Hủy',
      onOk() {
        deleteTicketType(ticket.id)
      },
    })
  }

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
          onClick={handleCreate}
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

      {/* Toolbar: Search & Filter */}
      <div 
        style={{ 
          background: '#fff', 
          padding: '20px 24px', 
          borderRadius: 16, 
          boxShadow: '0 8px 24px rgba(0,0,0,0.04)',
          marginBottom: 24, 
          display: 'flex', 
          gap: 16, 
          flexWrap: 'wrap',
          alignItems: 'center'
        }}
      >
        <Input
          placeholder="Tìm kiếm theo tên vé..."
          prefix={<SearchOutlined style={{ color: '#bfbfbf' }} />}
          allowClear
          size="large"
          value={searchTerm}
          onChange={(e) => {
            setSearchTerm(e.target.value)
            setPageIndex(1) // Reset về trang 1 khi search
          }}
          style={{ width: 320, borderRadius: 8 }}
        />
        <Select
          placeholder="Phân loại vé"
          allowClear
          size="large"
          value={category}
          onChange={(val) => {
            setCategory(val)
            setPageIndex(1)
          }}
          options={TicketCategoryOptions}
          style={{ width: 200, borderRadius: 8 }}
        />
        <Select
          placeholder="Trạng thái"
          allowClear
          size="large"
          value={isActive}
          onChange={(val) => {
            setIsActive(val)
            setPageIndex(1)
          }}
          options={[
            { label: 'Đang hoạt động', value: true },
            { label: 'Đã khóa', value: false },
          ]}
          style={{ width: 180, borderRadius: 8 }}
        />
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

      {!isLoading && !isError && ticketTypes.length === 0 && (
        <Empty
          description="Không tìm thấy loại vé nào phù hợp."
          style={{ padding: 48 }}
        />
      )}

      {!isLoading && !isError && ticketTypes.length > 0 && (
        <>
          <Row gutter={[24, 24]}>
            {ticketTypes.map((tt) => (
              <Col key={tt.id} xs={24} sm={12} lg={8} xl={6}>
                <TicketTypeCard 
                  ticketType={tt} 
                  onEdit={handleEdit}
                  onView={handleView}
                  onToggleLock={handleToggleLock}
                  onDelete={handleDelete}
                />
              </Col>
            ))}
          </Row>
          
          <div style={{ marginTop: 32, textAlign: 'right' }}>
            <Pagination
              current={pageIndex}
              pageSize={pageSize}
              total={totalCount}
              showSizeChanger
              pageSizeOptions={['4', '8', '12', '24']}
              onChange={(page, size) => {
                setPageIndex(page)
                setPageSize(size)
              }}
              showTotal={(total, range) => `${range[0]}-${range[1]} của ${total} loại vé`}
            />
          </div>
        </>
      )}

      {/* Modals */}
      <TicketTypeFormModal
        open={formModalOpen}
        onClose={() => setFormModalOpen(false)}
        editingTicket={selectedTicket}
      />

      <TicketTypeDetailModal
        open={detailModalOpen}
        onClose={() => setDetailModalOpen(false)}
        ticketType={selectedTicket}
      />
    </div>
  )
}
