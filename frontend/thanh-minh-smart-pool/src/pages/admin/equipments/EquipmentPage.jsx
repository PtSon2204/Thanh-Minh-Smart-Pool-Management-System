import { useEffect, useState } from 'react'
import { Alert, Button, ConfigProvider, Empty, Input, Pagination, Popconfirm, Select, Spin, Table, Tag } from 'antd'
import { EditOutlined, EnvironmentOutlined, EyeOutlined, FilterOutlined, LockOutlined, PlusOutlined, ReloadOutlined, SearchOutlined, ToolOutlined, UnlockOutlined } from '@ant-design/icons'
import { useEquipmentFilters, useEquipments } from "../../../features/equipments/hooks/useEquipments"
import { useSetEquipmentStatus } from "../../../features/equipments/hooks/useEquipmentMutations"
import { equipmentStatuses } from "../../../features/equipments/utils/equipmentDisplay"
import EquipmentFormModal from "../../../features/equipments/components/EquipmentFormModal"
import EquipmentUpdateModal from "../../../features/equipments/components/EquipmentUpdateModal"
import EquipmentDetailsModal from "../../../features/equipments/components/EquipmentDetailsModal"
import CameraViewModal from "../../../features/equipments/components/CameraViewModal"
import "../../../features/services/styles/serviceManagement.css"
import "../../../components/shared/styles/adminActions.css"
import "../../../features/equipments/styles/equipmentManagement.css"

const equipmentTheme = {
  token: { colorPrimary: '#0068ff', colorInfo: '#0068ff', borderRadius: 8 },
}

function EquipmentName({ equipment }) {
  return <div className="equipment-name">
    <strong>{equipment.name}</strong>
  </div>
}

function EquipmentStatus({ status }) {
  const display = equipmentStatuses[status]
  return <Tag className="equipment-status" color={display?.color || 'default'}><span className="equipment-status-dot" aria-hidden="true" />{display?.label || status || 'Chưa xác định'}</Tag>
}

function EquipmentActions({ equipment, onView, onEdit, statusMutation }) {
  const locked = equipment.status === 'Inactive'
  return <div className="admin-action-group equipment-actions">
    <Button className="admin-action-button admin-action-button--view" icon={<EyeOutlined />} aria-label={`Chi tiết ${equipment.name}`} onClick={() => onView(equipment)}>Chi tiết</Button>
    <Button className="admin-action-button admin-action-button--edit" icon={<EditOutlined />} aria-label={`Cập nhật ${equipment.name}`} disabled={statusMutation.isPending} onClick={() => onEdit(equipment)}>Cập nhật</Button>
    <Popconfirm title={locked ? 'Mở khóa thiết bị?' : 'Khóa thiết bị?'}
      description={locked ? `Cho phép sử dụng lại ${equipment.name}?` : `Ngừng sử dụng ${equipment.name} cho đến khi mở khóa?`}
      okText={locked ? 'Mở khóa' : 'Khóa thiết bị'} cancelText="Hủy" placement="bottomRight"
      onConfirm={() => statusMutation.mutate({ id: equipment.id, status: locked ? 'Active' : 'Inactive' })}>
      <Button className={`admin-action-button ${locked ? 'admin-action-button--positive' : 'admin-action-button--warning'}`}
        icon={locked ? <UnlockOutlined /> : <LockOutlined />} aria-label={`${locked ? 'Mở khóa' : 'Khóa'} ${equipment.name}`}
        loading={statusMutation.isPending && statusMutation.variables?.id === equipment.id} disabled={statusMutation.isPending}>
        {locked ? 'Mở khóa' : 'Khóa'}
      </Button>
    </Popconfirm>
  </div>
}

export default function EquipmentPage() {
  const [search, setSearch] = useState('')
  const [formOpen, setFormOpen] = useState(false)
  const [editingEquipment, setEditingEquipment] = useState(null)
  const [viewingEquipment, setViewingEquipment] = useState(null)
  const [viewingCamera, setViewingCamera] = useState(null)
  const [params, setParams] = useState({ searchTerm: '', pageIndex: 1, pageSize: 10 })
  const list = useEquipments(params)
  const filters = useEquipmentFilters()
  const statusMutation = useSetEquipmentStatus()
  const searching = search.trim() !== params.searchTerm
  const filtered = Boolean(search.trim() || params.category || params.location || params.status)
  const items = list.data?.items || []
  const busy = list.isFetching || searching

  useEffect(() => {
    const timer = setTimeout(() => {
      setParams(previous => previous.searchTerm === search.trim() ? previous : { ...previous, searchTerm: search.trim(), pageIndex: 1 })
    }, 350)
    return () => clearTimeout(timer)
  }, [search])

  function setFilter(key, value) {
    setParams(previous => ({ ...previous, [key]: value, pageIndex: 1 }))
  }

  function resetFilters() {
    setSearch('')
    setParams(previous => ({ pageIndex: 1, pageSize: previous.pageSize, searchTerm: '' }))
  }

  function renderActions(equipment) {
    return <EquipmentActions equipment={equipment} onView={setViewingEquipment} onEdit={setEditingEquipment} statusMutation={statusMutation} />
  }

  const columns = [
    { title: 'Tên thiết bị', dataIndex: 'name', render: (_, equipment) => <EquipmentName equipment={equipment} /> },
    { title: 'Nhóm thiết bị', dataIndex: 'category', width: '16%', render: value => <span className="equipment-category">{value || '—'}</span> },
    { title: 'Khu vực', dataIndex: 'location', width: '17%', render: value => <span className={`equipment-location ${value ? '' : 'equipment-muted'}`}><EnvironmentOutlined aria-hidden="true" />{value || 'Chưa xác định'}</span> },
    { title: 'Trạng thái', dataIndex: 'status', width: 175, render: value => <EquipmentStatus status={value} /> },
    { title: 'Thao tác', key: 'actions', width: 310, render: (_, equipment) => renderActions(equipment) },
  ]

  const emptyState = <Empty image={Empty.PRESENTED_IMAGE_SIMPLE}
    description={<><strong>{filtered ? 'Không tìm thấy thiết bị phù hợp' : 'Chưa có thiết bị nào'}</strong><p>{filtered ? 'Thử thay đổi từ khóa hoặc đặt lại bộ lọc.' : 'Thêm thiết bị đầu tiên để bắt đầu quản lý.'}</p></>}>
    <Button type="primary" icon={filtered ? <ReloadOutlined /> : <PlusOutlined />} onClick={filtered ? resetFilters : () => setFormOpen(true)}>{filtered ? 'Đặt lại bộ lọc' : 'Thêm thiết bị'}</Button>
  </Empty>

  return <ConfigProvider theme={equipmentTheme}>
    <div className="equipment-list-page">
      <header className="equipment-page-header">
        <div className="equipment-page-heading">
          <span className="equipment-header-icon" aria-hidden="true"><ToolOutlined /></span>
          <div><h1>Quản lý thiết bị</h1><p>Theo dõi và quản lý thiết bị trong khu vực hồ bơi.</p></div>
        </div>
        <Button type="primary" size="large" icon={<PlusOutlined />} onClick={() => setFormOpen(true)}>Thêm thiết bị</Button>
      </header>

      <section className="equipment-filter-panel" aria-label="Tìm kiếm và lọc thiết bị">
        <div className="equipment-panel-heading"><h2><FilterOutlined aria-hidden="true" />Tìm kiếm & bộ lọc</h2><Button type="text" icon={<ReloadOutlined />} onClick={resetFilters} disabled={!filtered}>Đặt lại bộ lọc</Button></div>
        <div className="equipment-toolbar">
          <label className="equipment-search"><span>Tìm kiếm thiết bị</span><Input allowClear maxLength={255} prefix={<SearchOutlined />} aria-label="Tìm theo tên hoặc vị trí thiết bị" placeholder="Nhập tên hoặc khu vực thiết bị..." value={search} onChange={event => setSearch(event.target.value)} /></label>
          <label><span>Nhóm thiết bị</span><Select allowClear showSearch optionFilterProp="label" aria-label="Nhóm thiết bị" placeholder="Tất cả nhóm" value={params.category} loading={filters.isFetching} options={(filters.data?.categories || []).map(value => ({ value, label: value }))} onChange={value => setFilter('category', value)} /></label>
          <label><span>Khu vực</span><Select allowClear showSearch optionFilterProp="label" aria-label="Khu vực" placeholder="Tất cả khu vực" value={params.location} loading={filters.isFetching} options={(filters.data?.locations || []).map(value => ({ value, label: value }))} onChange={value => setFilter('location', value)} /></label>
          <label><span>Trạng thái</span><Select allowClear aria-label="Trạng thái thiết bị" placeholder="Tất cả trạng thái" value={params.status} loading={filters.isFetching} options={(filters.data?.statuses || []).map(value => ({ value, label: equipmentStatuses[value]?.label || value }))} onChange={value => setFilter('status', value)} /></label>
        </div>
      </section>

      {filters.isError && <Alert type="error" showIcon title="Không thể tải bộ lọc thiết bị" action={<Button onClick={() => filters.refetch()}>Thử lại</Button>} />}
      {statusMutation.isError && <Alert type="error" showIcon closable onClose={() => statusMutation.reset()} title={statusMutation.error?.response?.data?.message || 'Không thể thay đổi trạng thái thiết bị. Vui lòng thử lại.'} />}

      <section className="equipment-list-panel" aria-label="Danh sách thiết bị" aria-busy={busy}>
        <div className="equipment-panel-heading equipment-list-heading">
          <h2>Danh sách thiết bị{list.data && !list.isError && <span className="equipment-count">{list.data.totalCount.toLocaleString('vi-VN')}</span>}</h2>
          <Button type="text" icon={<ReloadOutlined spin={list.isFetching} />} disabled={busy} onClick={() => list.refetch()}>Làm mới</Button>
        </div>
        {list.isError ? <div className="equipment-error"><Alert type="error" showIcon title={list.error?.response?.status === 403 ? 'Bạn không có quyền xem danh sách thiết bị.' : 'Không thể tải danh sách thiết bị.'} action={<Button icon={<ReloadOutlined />} onClick={() => list.refetch()}>Thử lại</Button>} /></div> : <>
          <Spin spinning={busy}>
            <Table className="equipment-desktop-table" rowKey="id" columns={columns} dataSource={items} tableLayout="fixed" pagination={false} locale={{ emptyText: busy ? 'Đang tải thiết bị...' : emptyState }} />
            <div className="equipment-mobile-list">
              {items.map(equipment => <article className="equipment-mobile-card" key={equipment.id}>
                <EquipmentName equipment={equipment} />
                <dl><div><dt>Nhóm thiết bị</dt><dd><span className="equipment-category">{equipment.category}</span></dd></div><div><dt>Trạng thái</dt><dd><EquipmentStatus status={equipment.status} /></dd></div><div className="equipment-card-location"><dt>Khu vực</dt><dd><EnvironmentOutlined aria-hidden="true" /> {equipment.location || 'Chưa xác định'}</dd></div></dl>
                {renderActions(equipment)}
              </article>)}
              {!items.length && (busy ? <div className="equipment-loading">Đang tải thiết bị...</div> : emptyState)}
            </div>
          </Spin>
          <footer className="equipment-pagination">
            <Pagination current={params.pageIndex} pageSize={params.pageSize} total={list.data?.totalCount || 0} showSizeChanger pageSizeOptions={[10, 20, 50, 100]}
              locale={{ items_per_page: '/ trang', prev_page: 'Trang trước', next_page: 'Trang sau', jump_to: 'Đến trang', page: '' }}
              showTotal={(total, range) => total ? `${range[0]}–${range[1]} trong ${total.toLocaleString('vi-VN')} thiết bị` : 'Tổng 0 thiết bị'}
              onChange={(pageIndex, pageSize) => setParams(previous => ({ ...previous, pageIndex: pageSize === previous.pageSize ? pageIndex : 1, pageSize }))} />
          </footer>
        </>}
      </section>

      <EquipmentFormModal open={formOpen} onClose={() => setFormOpen(false)} />
      <EquipmentUpdateModal equipment={editingEquipment} onClose={() => setEditingEquipment(null)} />
      <EquipmentDetailsModal equipment={viewingEquipment && (items.find(item => item.id === viewingEquipment.id) || viewingEquipment)} onClose={() => setViewingEquipment(null)}
        onEdit={equipment => { setViewingEquipment(null); setEditingEquipment(equipment) }}
        onViewCamera={equipment => { setViewingEquipment(null); setViewingCamera(equipment) }} />
      {viewingCamera && <CameraViewModal key={viewingCamera.id} equipment={viewingCamera} onClose={() => setViewingCamera(null)} />}
    </div>
  </ConfigProvider>
}

