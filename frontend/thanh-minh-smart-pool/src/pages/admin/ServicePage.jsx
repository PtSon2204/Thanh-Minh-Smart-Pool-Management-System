import { useState } from 'react'
import { CheckCircleOutlined, EditOutlined, PauseCircleOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons'
import { Alert, Button, Empty, Pagination, Popconfirm, Select, Spin, Table, Tag } from 'antd'
import ServiceFormModal from '../../features/services/components/ServiceFormModal'
import { ServiceCategoryTabs, ServiceListHeader, ServiceListToolbar, ServiceNameCell } from '../../features/services/components/ServiceListChrome'
import { useServices } from '../../features/services/hooks/useServices'
import { useSetServiceStatus } from '../../features/services/hooks/useServiceMutations'
import '../../features/services/styles/serviceManagement.css'
import '../../features/shared/styles/adminActions.css'
import '../../features/shared/styles/serviceMobileCards.css'

const currencyFormatter = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })

function RetryEmpty({ onRetry }) {
  return <Empty description="Không thể tải danh sách dịch vụ."><Button icon={<ReloadOutlined />} onClick={onRetry}>Thử lại</Button></Empty>
}

function ServiceActions({ service, onEdit, statusMutation }) {
  return <div className="admin-action-group">
    <Button className="admin-action-button admin-action-button--edit" icon={<EditOutlined />} aria-label={`Sửa dịch vụ ${service.name}`} onClick={() => onEdit(service)}>Sửa</Button>
    <Popconfirm
      placement="bottomRight"
      title={service.isActive ? 'Tạm dừng dịch vụ?' : 'Kích hoạt dịch vụ?'}
      description={`Bạn có muốn ${service.isActive ? 'tạm dừng' : 'kích hoạt'} ${service.name}?`}
      okText={service.isActive ? 'Tạm dừng' : 'Kích hoạt'}
      cancelText="Hủy"
      onConfirm={() => statusMutation.mutate({ id: service.id, isActive: !service.isActive })}
    >
      <Button
        className={`admin-action-button ${service.isActive ? 'admin-action-button--warning' : 'admin-action-button--positive'}`}
        icon={service.isActive ? <PauseCircleOutlined /> : <CheckCircleOutlined />}
        loading={statusMutation.isPending && statusMutation.variables?.id === service.id}
        disabled={statusMutation.isPending}
      >
        {service.isActive ? 'Tạm dừng' : 'Kích hoạt'}
      </Button>
    </Popconfirm>
  </div>
}

function ServiceMobileCards({ services, onEdit, statusMutation }) {
  return <div className="service-mobile-list">{services.map((service) => <article className="service-mobile-card" key={service.id}>
    <div className="service-mobile-card-heading"><ServiceNameCell name={service.name} type={service.type} /><Tag color={service.isActive ? 'success' : 'error'}>{service.isActive ? 'Hoạt động' : 'Tạm dừng'}</Tag></div>
    <dl className="service-mobile-card-details">
      <div><dt>Đơn giá</dt><dd>{currencyFormatter.format(service.price)}</dd></div>
      <div><dt>Tồn kho</dt><dd>{service.stockQuantity ?? 'Không theo dõi'}</dd></div>
    </dl>
    <ServiceActions service={service} onEdit={onEdit} statusMutation={statusMutation} />
  </article>)}</div>
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
  const statusMutation = useSetServiceStatus()

  const resetPage = (setter) => (value) => {
    setter(value)
    setPageIndex(1)
  }

  const handleCategoryChange = (category) => {
    resetPage(setType)(category === 'all' ? undefined : category)
  }

  const openEdit = (service) => {
    setEditingService(service)
    setFormOpen(true)
  }

  const columns = [
    {
      title: 'Dịch vụ', dataIndex: 'name', key: 'name', render: (name, service) => <ServiceNameCell name={name} type={service.type} />,
    },
    { title: 'Đơn giá', dataIndex: 'price', key: 'price', align: 'right', render: (value) => <span className="service-number-cell">{currencyFormatter.format(value)}</span> },
    { title: 'Tồn kho', dataIndex: 'stockQuantity', key: 'stockQuantity', align: 'right', render: (value) => <span className="service-number-cell">{value ?? 'Không theo dõi'}</span> },
    { title: 'Trạng thái', dataIndex: 'isActive', key: 'isActive', render: (value) => <Tag color={value ? 'success' : 'error'}>{value ? 'Hoạt động' : 'Tạm dừng'}</Tag> },
    {
      title: 'Thao tác', key: 'actions', align: 'right', render: (_, service) => <ServiceActions service={service} onEdit={openEdit} statusMutation={statusMutation} />,
    },
  ]

  return (
    <div className="service-list-page">
      <ServiceListHeader title="Dịch vụ" tabs={<ServiceCategoryTabs value={type || 'all'} onChange={handleCategoryChange} />} action={<Button type="primary" icon={<PlusOutlined />} onClick={() => { setEditingService(null); setFormOpen(true) }}>Thêm dịch vụ</Button>} />
      <ServiceListToolbar searchTerm={searchTerm} onSearch={resetPage(setSearchTerm)}>
        <label className="service-filter-control"><span>Trạng thái</span><Select allowClear aria-label="Lọc theo trạng thái dịch vụ" placeholder="Tất cả" options={[{ label: 'Hoạt động', value: true }, { label: 'Tạm dừng', value: false }]} value={isActive} onChange={resetPage(setIsActive)} /></label>
      </ServiceListToolbar>
      {statusMutation.error && <Alert className="service-inline-error" type="error" showIcon message="Không thể cập nhật trạng thái" description={statusMutation.error?.response?.data?.message || 'Máy chủ không thể cập nhật trạng thái dịch vụ.'} />}
      <section className="service-list-table service-catalogue-list" aria-label="Danh sách dịch vụ">
        {servicesQuery.isLoading && <div className="admin-management-state"><Spin size="large" /></div>}
        {servicesQuery.isError && <RetryEmpty onRetry={servicesQuery.refetch} />}
        {!servicesQuery.isLoading && !servicesQuery.isError && <>
          <Table className="service-desktop-table" rowKey="id" columns={columns} dataSource={servicesQuery.data?.items || []} pagination={false} scroll={{ x: true }} locale={{ emptyText: 'Không tìm thấy dịch vụ phù hợp.' }} />
          <ServiceMobileCards services={servicesQuery.data?.items || []} onEdit={openEdit} statusMutation={statusMutation} />
        </>}
        {!servicesQuery.isLoading && !servicesQuery.isError && (servicesQuery.data?.totalCount || 0) > 0 && <div className="admin-management-pagination"><Pagination current={pageIndex} pageSize={pageSize} total={servicesQuery.data.totalCount} showSizeChanger pageSizeOptions={['10', '20', '50', '100']} onChange={(page, size) => { setPageIndex(page); setPageSize(size) }} showTotal={(total, range) => `${range[0]}-${range[1]} của ${total} dịch vụ`} /></div>}
      </section>
      <ServiceFormModal open={formOpen} onClose={() => setFormOpen(false)} service={editingService} />
    </div>
  )
}
