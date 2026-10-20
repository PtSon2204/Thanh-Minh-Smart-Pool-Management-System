import { useEffect, useState } from 'react'
import { Alert, Button, Input, Select, Table, Tag, Tooltip } from 'antd'
import { PlusOutlined, ReloadOutlined, SearchOutlined, VideoCameraOutlined } from '@ant-design/icons'
import dayjs from 'dayjs'
import { ServiceListHeader } from '../../features/services/components/ServiceListChrome'
import { useEquipmentFilters, useEquipments } from '../../features/equipments/hooks/useEquipments'
import EquipmentFormModal from '../../features/equipments/components/EquipmentFormModal'
import CameraViewModal from '../../features/equipments/components/CameraViewModal'
import '../../features/services/styles/serviceManagement.css'
import '../../features/equipments/styles/equipmentManagement.css'

const equipmentStatuses = {
  Active: { label: 'Đang hoạt động', color: 'success' },
  Maintenance: { label: 'Bảo trì', color: 'warning' },
  Inactive: { label: 'Ngừng sử dụng', color: 'default' },
}

const cameraStates = {
  Connected: { label: 'Camera đã kết nối', color: 'success' },
  Connecting: { label: 'Camera đang kết nối', color: 'processing' },
  Disconnected: { label: 'Camera mất kết nối', color: 'default' },
  Error: { label: 'Lỗi kết nối camera', color: 'error' },
}

function statusLabel(value) {
  return equipmentStatuses[value]?.label || value || 'Chưa xác định'
}

function formatMetadata(value) {
  if (!value) return '—'
  try {
    const metadata = JSON.parse(value)
    if (metadata && typeof metadata === 'object' && !Array.isArray(metadata)) {
      return Object.entries(metadata).map(([key, item]) => `${key}: ${typeof item === 'object' ? JSON.stringify(item) : String(item)}`).join('; ') || '—'
    }
    return typeof metadata === 'string' ? metadata : JSON.stringify(metadata)
  } catch {
    return value
  }
}

export default function EquipmentPage() {
  const [search, setSearch] = useState('')
  const [formOpen, setFormOpen] = useState(false)
  const [viewingCamera, setViewingCamera] = useState(null)
  const [params, setParams] = useState({ searchTerm: '', pageIndex: 1, pageSize: 10 })
  const list = useEquipments(params)
  const filters = useEquipmentFilters()
  const searching = search.trim() !== params.searchTerm
  const filtered = Boolean(params.searchTerm || params.category || params.location || params.status)

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

  const columns = [
    { title: 'Tên thiết bị', dataIndex: 'name', width: 190 },
    { title: 'Nhóm thiết bị', dataIndex: 'category', width: 150 },
    { title: 'Khu vực/Vị trí', dataIndex: 'location', width: 180, render: value => value || '—' },
    { title: 'Thông số riêng', dataIndex: 'metadata', width: 240, render: value => {
      const text = formatMetadata(value)
      return <Tooltip title={text}><span className="equipment-specification">{text}</span></Tooltip>
    } },
    { title: 'Trạng thái', dataIndex: 'status', width: 180, render: (value, equipment) => <>
      <Tag color={equipmentStatuses[value]?.color || 'default'}>{statusLabel(value)}</Tag>
      {equipment.cameraConnection && <Tag color={cameraStates[equipment.cameraConnection.state]?.color || 'default'}>{cameraStates[equipment.cameraConnection.state]?.label || 'Camera mất kết nối'}</Tag>}
    </> },
    { title: 'Bảo trì sắp tới', dataIndex: 'nextMaintenanceDate', width: 190, render: value => value ? dayjs(value).format('DD/MM/YYYY') : 'Chưa có lịch bảo trì' },
    { title: 'Thao tác', key: 'actions', width: 160, render: (_, equipment) => equipment.category?.trim().toLowerCase() === 'camera'
      ? <Button icon={<VideoCameraOutlined />} onClick={() => setViewingCamera(equipment)}>Xem Camera</Button>
      : <span aria-label="Chưa có thao tác khả dụng">—</span> },
  ]

  return <div className="service-list-page equipment-list-page">
    <ServiceListHeader title="Quản lý thiết bị" action={<Button type="primary" icon={<PlusOutlined />} onClick={() => setFormOpen(true)}>Thêm thiết bị</Button>} />
    <section className="equipment-toolbar" aria-label="Tìm kiếm và lọc thiết bị">
      <label className="equipment-search"><span>Tìm kiếm</span><Input allowClear maxLength={255} prefix={<SearchOutlined />} aria-label="Tìm theo tên hoặc vị trí thiết bị" placeholder="Nhập tên hoặc vị trí thiết bị" value={search} onChange={event => setSearch(event.target.value)} /></label>
      <label><span>Nhóm thiết bị</span><Select allowClear showSearch optionFilterProp="label" aria-label="Nhóm thiết bị" placeholder="Tất cả nhóm" value={params.category} loading={filters.isFetching} options={(filters.data?.categories || []).map(value => ({ value, label: value }))} onChange={value => setFilter('category', value)} /></label>
      <label><span>Khu vực/Vị trí</span><Select allowClear showSearch optionFilterProp="label" aria-label="Khu vực/Vị trí" placeholder="Tất cả vị trí" value={params.location} loading={filters.isFetching} options={(filters.data?.locations || []).map(value => ({ value, label: value }))} onChange={value => setFilter('location', value)} /></label>
      <label><span>Trạng thái</span><Select allowClear aria-label="Trạng thái thiết bị" placeholder="Tất cả trạng thái" value={params.status} loading={filters.isFetching} options={(filters.data?.statuses || []).map(value => ({ value, label: statusLabel(value) }))} onChange={value => setFilter('status', value)} /></label>
      <Button icon={<ReloadOutlined />} onClick={resetFilters}>Đặt lại bộ lọc</Button>
    </section>
    {filters.isError && <Alert type="error" showIcon title="Không thể tải bộ lọc thiết bị" action={<Button onClick={() => filters.refetch()}>Thử lại</Button>} />}
    <section className="service-list-table" aria-label="Danh sách thiết bị" aria-busy={list.isFetching || searching}>
      {list.isError ? <Alert type="error" showIcon title={list.error?.response?.status === 403 ? 'Bạn không có quyền xem danh sách thiết bị.' : 'Không thể tải danh sách thiết bị.'} action={<Button icon={<ReloadOutlined />} onClick={() => list.refetch()}>Thử lại</Button>} /> :
        <Table rowKey="id" columns={columns} dataSource={list.data?.items || []} loading={list.isFetching || searching} scroll={{ x: 1200 }}
          locale={{ emptyText: filtered ? 'Không tìm thấy thiết bị phù hợp' : 'Chưa có thiết bị' }}
          pagination={{
            current: params.pageIndex, pageSize: params.pageSize, total: list.data?.totalCount || 0,
            showSizeChanger: true, pageSizeOptions: [10, 20, 50, 100],
            showTotal: total => `Tổng ${total} thiết bị`,
            onChange: (pageIndex, pageSize) => setParams(previous => ({ ...previous, pageIndex: pageSize === previous.pageSize ? pageIndex : 1, pageSize })),
          }} />}
    </section>
    <EquipmentFormModal open={formOpen} onClose={() => setFormOpen(false)} />
    {viewingCamera && <CameraViewModal key={viewingCamera.id} equipment={viewingCamera} onClose={() => setViewingCamera(null)} />}
  </div>
}
