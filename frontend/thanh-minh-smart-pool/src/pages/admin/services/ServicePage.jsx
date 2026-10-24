import { useState } from 'react'
import { CheckCircleOutlined, EditOutlined, HistoryOutlined, InboxOutlined, PauseCircleOutlined, PlusOutlined, ReloadOutlined } from '@ant-design/icons'
import { Alert, Button, Empty, Pagination, Popconfirm, Select, Spin, Table, Tag, Tooltip } from 'antd'
import ServiceFormModal from "../../../features/services/components/ServiceFormModal"
import InventoryControls from "../../../features/services/components/InventoryControls"
import { ServiceListHeader, ServiceListToolbar, ServiceNameCell } from "../../../features/services/components/ServiceListChrome"
import { useServices } from "../../../features/services/hooks/useServices"
import { useSetServiceStatus } from "../../../features/services/hooks/useServiceMutations"
import { useAuthStore } from "../../../features/auth/store/authStore"
import "../../../features/services/styles/serviceManagement.css"
import "../../../components/shared/styles/adminActions.css"
import "../../../components/shared/styles/serviceMobileCards.css"

const currencyFormatter = new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' })

function RetryEmpty({ onRetry }) {
  return <Empty description="Không thể tải danh sách dịch vụ."><Button icon={<ReloadOutlined />} onClick={onRetry}>Thử lại</Button></Empty>
}

function ServiceActions({ service, onEdit, statusMutation, canManage, inventory }) {
  return <div className="admin-action-group service-item-actions">
    {canManage && <>
    <Tooltip title="Sửa đồ cho thuê" trigger={['hover', 'focus']}>
      <Button className="admin-action-button admin-action-button--edit" icon={<EditOutlined />} aria-label={`Sửa dịch vụ ${service.name}`} onClick={() => onEdit(service)} />
    </Tooltip>
    <Popconfirm
      placement="bottomRight"
      title={service.isActive ? 'Tạm dừng dịch vụ?' : 'Kích hoạt dịch vụ?'}
      description={`Bạn có muốn ${service.isActive ? 'tạm dừng' : 'kích hoạt'} ${service.name}?`}
      okText={service.isActive ? 'Tạm dừng' : 'Kích hoạt'}
      cancelText="Hủy"
      onConfirm={() => statusMutation.mutate({ id: service.id, isActive: !service.isActive })}
    >
      <Tooltip title={service.isActive ? 'Tạm dừng' : 'Kích hoạt'} trigger={['hover', 'focus']}>
      <Button
        className={`admin-action-button ${service.isActive ? 'admin-action-button--warning' : 'admin-action-button--positive'}`}
        icon={service.isActive ? <PauseCircleOutlined /> : <CheckCircleOutlined />}
        loading={statusMutation.isPending && statusMutation.variables?.id === service.id}
        disabled={statusMutation.isPending}
        aria-label={`${service.isActive ? 'Tạm dừng' : 'Kích hoạt'} ${service.name}`}
      />
      </Tooltip>
    </Popconfirm>
    </>}
    <Tooltip title={service.stockQuantity == null ? 'Đồ cho thuê này chưa theo dõi tồn kho' : 'Điều chỉnh tồn kho'} trigger={['hover', 'focus']}>
      <Button className="admin-action-button admin-action-button--primary" icon={<InboxOutlined />} disabled={service.stockQuantity == null || inventory.isAdjusting} aria-label={`Điều chỉnh tồn kho ${service.name}`} onClick={() => inventory.onAdjust(service)} />
    </Tooltip>
    <Tooltip title="Lịch sử tồn kho" trigger={['hover', 'focus']}>
      <Button className="admin-action-button admin-action-button--view" icon={<HistoryOutlined />} disabled={inventory.isAdjusting} aria-label={`Lịch sử tồn kho ${service.name}`} onClick={() => inventory.onHistory(service)} />
    </Tooltip>
  </div>
}

function ServiceMobileCards({ services, onEdit, statusMutation, canManage, inventory }) {
  return <div className="service-mobile-list">{services.map((service) => <article className="service-mobile-card" key={service.id}>
    <div className="service-mobile-card-heading"><ServiceNameCell name={service.name} /><Tag color={service.isActive ? 'success' : 'error'}>{service.isActive ? 'Hoạt động' : 'Tạm dừng'}</Tag></div>
    <dl className="service-mobile-card-details">
      <div><dt>Giá thuê</dt><dd>{currencyFormatter.format(service.price)}</dd></div>
      <div><dt>Có thể cho thuê</dt><dd>{service.stockQuantity ?? 'Chưa khai báo'}</dd></div>
    </dl>
    <ServiceActions service={service} onEdit={onEdit} statusMutation={statusMutation} canManage={canManage} inventory={inventory} />
  </article>)}</div>
}

export default function ServicePage() {
  const canManage = useAuthStore((state) => state.session?.role?.toUpperCase() === 'ADMIN')
  const [searchTerm, setSearchTerm] = useState('')
  const [isActive, setIsActive] = useState()
  const [pageIndex, setPageIndex] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [formOpen, setFormOpen] = useState(false)
  const [editingService, setEditingService] = useState(null)
  const servicesQuery = useServices({ searchTerm: searchTerm || undefined, isActive, pageIndex, pageSize })
  const statusMutation = useSetServiceStatus()

  const resetPage = (setter) => (value) => {
    setter(value)
    setPageIndex(1)
  }

  const openEdit = (service) => {
    setEditingService(service)
    setFormOpen(true)
  }

  const columns = (inventory) => [
    {
      title: 'Đồ cho thuê', dataIndex: 'name', key: 'name', width: '36%', render: (name) => <ServiceNameCell name={name} />,
    },
    { title: 'Giá thuê', dataIndex: 'price', key: 'price', width: '12%', align: 'right', render: (value) => <span className="service-number-cell">{currencyFormatter.format(value)}</span> },
    { title: 'Có thể cho thuê', dataIndex: 'stockQuantity', key: 'stockQuantity', width: '14%', align: 'right', render: (value) => <span className="service-number-cell">{value ?? 'Chưa khai báo'}</span> },
    { title: 'Trạng thái', dataIndex: 'isActive', key: 'isActive', width: '14%', render: (value) => <Tag color={value ? 'success' : 'error'}>{value ? 'Hoạt động' : 'Tạm dừng'}</Tag> },
    {
       title: 'Thao tác', key: 'actions', width: '24%', align: 'right', render: (_, service) => <ServiceActions service={service} onEdit={openEdit} statusMutation={statusMutation} canManage={canManage} inventory={inventory} />,
    },
  ]

  return (
    <InventoryControls>{(inventory) => <div className="service-list-page service-items-page">
      <ServiceListHeader title="Đồ cho thuê" action={canManage && <Button type="primary" icon={<PlusOutlined />} onClick={() => { setEditingService(null); setFormOpen(true) }}>Thêm đồ cho thuê</Button>} />
      <ServiceListToolbar searchTerm={searchTerm} onSearch={resetPage(setSearchTerm)} searchLabel="Tìm đồ cho thuê">
        <label className="service-filter-control"><span>Trạng thái</span><Select aria-label="Lọc theo trạng thái dịch vụ" options={[{ label: 'Tất cả', value: 'all' }, { label: 'Hoạt động', value: 'active' }, { label: 'Tạm dừng', value: 'inactive' }]} value={isActive == null ? 'all' : isActive ? 'active' : 'inactive'} onChange={(value) => resetPage(setIsActive)(value === 'all' ? undefined : value === 'active')} /></label>
      </ServiceListToolbar>
      {statusMutation.error && <Alert className="service-inline-error" type="error" showIcon message="Không thể cập nhật trạng thái" description={statusMutation.error?.response?.data?.message || 'Máy chủ không thể cập nhật trạng thái dịch vụ.'} />}
      <section className="service-list-table service-catalogue-list" aria-label="Danh sách dịch vụ">
        {servicesQuery.isLoading && <div className="admin-management-state"><Spin size="large" /></div>}
        {servicesQuery.isError && <RetryEmpty onRetry={servicesQuery.refetch} />}
        {!servicesQuery.isLoading && !servicesQuery.isError && <>
          <Table className="service-desktop-table" rowKey="id" columns={columns(inventory)} dataSource={servicesQuery.data?.items || []} pagination={false} tableLayout="fixed" locale={{ emptyText: 'Không tìm thấy dịch vụ phù hợp.' }} />
          <ServiceMobileCards services={servicesQuery.data?.items || []} onEdit={openEdit} statusMutation={statusMutation} canManage={canManage} inventory={inventory} />
        </>}
        {!servicesQuery.isLoading && !servicesQuery.isError && (servicesQuery.data?.totalCount || 0) > 0 && <div className="admin-management-pagination"><Pagination current={pageIndex} pageSize={pageSize} total={servicesQuery.data.totalCount} showSizeChanger pageSizeOptions={['10', '20', '50', '100']} onChange={(page, size) => { setPageIndex(page); setPageSize(size) }} showTotal={(total, range) => `${range[0]}-${range[1]} của ${total} dịch vụ`} /></div>}
      </section>
      <ServiceFormModal open={canManage && formOpen} onClose={() => setFormOpen(false)} service={editingService} />
    </div>}</InventoryControls>
  )
}
