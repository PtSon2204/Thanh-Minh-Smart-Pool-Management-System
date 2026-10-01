import { useState } from 'react'
import { EditOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons'
import { Button, Empty, Pagination, Select, Spin, Table, Tag } from 'antd'
import ServiceFormModal from '../../features/services/components/ServiceFormModal'
import { ServiceCategoryTabs, ServiceListHeader, ServiceListToolbar, ServiceNameCell } from '../../features/services/components/ServiceListChrome'
import { useServices } from '../../features/services/hooks/useServices'
import '../../features/services/styles/serviceManagement.css'

const currencyFormatter = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })

function RetryEmpty({ onRetry }) {
  return <Empty description="Không thể tải danh sách dịch vụ."><Button icon={<ReloadOutlined />} onClick={onRetry}>Thử lại</Button></Empty>
}

export default function ServicePage() {
  const [searchTerm, setSearchTerm] = useState('')
  const [type, setType] = useState()
  const [isActive, setIsActive] = useState()
  const [pageIndex, setPageIndex] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [formOpen, setFormOpen] = useState(false)
  const [editingService, setEditingService] = useState(null)
  const servicesQuery = useServices({ searchTerm: searchTerm || undefined, type, isActive, pageIndex, pageSize })

  const resetPage = (setter) => (value) => {
    setter(value)
    setPageIndex(1)
  }

  const handleCategoryChange = (category) => {
    resetPage(setType)(category === 'all' ? undefined : category)
  }

  const columns = [
    {
      title: 'Dịch vụ', dataIndex: 'name', key: 'name', render: (name, service) => <ServiceNameCell name={name} type={service.type} />,
    },
    { title: 'Đơn giá', dataIndex: 'price', key: 'price', align: 'right', render: (value) => <span className="service-number-cell">{currencyFormatter.format(value)}</span> },
    { title: 'Tồn kho', dataIndex: 'stockQuantity', key: 'stockQuantity', align: 'right', render: (value) => <span className="service-number-cell">{value ?? 'Không theo dõi'}</span> },
    { title: 'Trạng thái', dataIndex: 'isActive', key: 'isActive', render: (value) => <Tag color={value ? 'success' : 'error'}>{value ? 'Hoạt động' : 'Tạm dừng'}</Tag> },
    {
      title: 'Thao tác', key: 'actions', align: 'right', render: (_, service) => <Button type="text" icon={<EditOutlined />} aria-label={`Sửa dịch vụ ${service.name}`} onClick={() => { setEditingService(service); setFormOpen(true) }}>Sửa</Button>,
    },
  ]

  return (
    <div className="service-list-page">
      <ServiceListHeader title="Dịch vụ" tabs={<ServiceCategoryTabs value={type || 'all'} onChange={handleCategoryChange} />} action={<Button type="primary" icon={<PlusOutlined />} onClick={() => { setEditingService(null); setFormOpen(true) }}>Thêm dịch vụ</Button>} />
      <ServiceListToolbar searchTerm={searchTerm} onSearch={resetPage(setSearchTerm)}>
        <label className="service-filter-control"><span>Trạng thái</span><Select allowClear aria-label="Lọc theo trạng thái dịch vụ" placeholder="Tất cả" options={[{ label: 'Hoạt động', value: true }, { label: 'Tạm dừng', value: false }]} value={isActive} onChange={resetPage(setIsActive)} /></label>
      </ServiceListToolbar>
      <section className="service-list-table" aria-label="Danh sách dịch vụ">
        {servicesQuery.isLoading && <div className="admin-management-state"><Spin size="large" /></div>}
        {servicesQuery.isError && <RetryEmpty onRetry={servicesQuery.refetch} />}
        {!servicesQuery.isLoading && !servicesQuery.isError && <Table rowKey="id" columns={columns} dataSource={servicesQuery.data?.items || []} pagination={false} scroll={{ x: true }} locale={{ emptyText: 'Không tìm thấy dịch vụ phù hợp.' }} />}
        {!servicesQuery.isLoading && !servicesQuery.isError && (servicesQuery.data?.totalCount || 0) > 0 && <div className="admin-management-pagination"><Pagination current={pageIndex} pageSize={pageSize} total={servicesQuery.data.totalCount} showSizeChanger pageSizeOptions={['10', '20', '50', '100']} onChange={(page, size) => { setPageIndex(page); setPageSize(size) }} showTotal={(total, range) => `${range[0]}-${range[1]} của ${total} dịch vụ`} /></div>}
      </section>
      <ServiceFormModal open={formOpen} onClose={() => setFormOpen(false)} service={editingService} />
    </div>
  )
}
