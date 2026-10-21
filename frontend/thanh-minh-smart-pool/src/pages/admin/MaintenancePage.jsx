import { useEffect, useState } from 'react'
import { Alert, Button, ConfigProvider, Dropdown, Empty, Input, Modal, Pagination, Select, Spin, Table, Tag, message } from 'antd'
import { CalendarOutlined, CheckCircleOutlined, ClockCircleOutlined, EditOutlined, ExclamationCircleOutlined, EyeOutlined, HistoryOutlined, MoreOutlined, PlusOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons'
import { Link } from 'react-router-dom'
import dayjs from 'dayjs'
import { useMaintenanceList, useMaintenanceMutation, useMaintenanceOptions } from '../../features/maintenance/hooks/useMaintenance'
import { maintenanceStatuses, recurrenceText, vietnamToday } from '../../features/maintenance/utils/maintenanceDisplay'
import MaintenanceFormModal from '../../features/maintenance/components/MaintenanceFormModal'
import MaintenanceDetailsModal from '../../features/maintenance/components/MaintenanceDetailsModal'
import CompleteMaintenanceModal from '../../features/maintenance/components/CompleteMaintenanceModal'
import '../../features/services/styles/serviceManagement.css'
import '../../features/shared/styles/adminActions.css'
import '../../features/equipments/styles/equipmentManagement.css'
import '../../features/maintenance/styles/maintenance.css'

function Status({ schedule }) {
  const status = maintenanceStatuses[schedule.status]
  return <div className="maintenance-status"><Tag color={status?.color || 'default'}>{status?.label || schedule.status}</Tag>{schedule.isOverdue && <Tag color="error">Quá hạn</Tag>}</div>
}

const actionMessages = { start: 'Đã bắt đầu bảo trì', pause: 'Đã tạm dừng lịch', resume: 'Đã tiếp tục lịch', cancel: 'Đã hủy lịch và dừng các lần lặp tiếp theo' }

export default function MaintenancePage() {
  const [params, setParams] = useState({ pageIndex: 1, pageSize: 10, status: 'Pending', searchTerm: '' })
  const [search, setSearch] = useState('')
  const [formOpen, setFormOpen] = useState(false)
  const [editing, setEditing] = useState(null)
  const [viewing, setViewing] = useState(null)
  const [completing, setCompleting] = useState(null)
  const [modal, contextHolder] = Modal.useModal()
  const list = useMaintenanceList(params)
  const options = useMaintenanceOptions()
  const mutation = useMaintenanceMutation()
  const page = list.data?.page
  const summary = list.data?.summary
  const items = page?.items || []
  const busy = list.isFetching || search.trim() !== params.searchTerm

  useEffect(() => {
    const timer = setTimeout(() => setParams(previous => previous.searchTerm === search.trim() ? previous : { ...previous, searchTerm: search.trim(), pageIndex: 1 }), 350)
    return () => clearTimeout(timer)
  }, [search])

  function changeFilter(key, value) { setParams(previous => ({ ...previous, [key]: value, pageIndex: 1 })) }
  function edit(row) { setViewing(null); setEditing(row); setFormOpen(true) }

  function changeState(row, action) {
    const titles = { start: 'Bắt đầu bảo trì thiết bị?', pause: 'Tạm dừng lịch bảo trì?', resume: 'Tiếp tục lịch bảo trì?', cancel: 'Hủy lịch bảo trì?' }
    const descriptions = {
      start: 'Công việc sẽ chuyển sang Đang thực hiện. Thiết bị đang hoạt động sẽ chuyển sang trạng thái Bảo trì.',
      pause: 'Lịch sẽ ngừng nhắc việc và chờ đến khi bạn tiếp tục.',
      resume: 'Lịch sẽ tiếp tục được theo dõi và nhắc việc.',
      cancel: 'Dừng công việc này và các lần lặp tiếp theo. Những lần đã thực hiện vẫn được giữ trong lịch sử.',
    }
    modal.confirm({ title: titles[action], content: descriptions[action], okText: 'Xác nhận', cancelText: 'Quay lại', okButtonProps: { danger: action === 'cancel' },
      onOk: async () => {
        try {
          await mutation.mutateAsync({ action, id: row.id })
          message.success(actionMessages[action])
          changeFilter('pageIndex', 1)
        } catch (error) { message.error(error?.response?.data?.message || 'Không thể thực hiện thao tác. Vui lòng thử lại.') }
      },
    })
  }

  function actions(row) {
    const editable = ['Pending', 'Paused'].includes(row.status)
    const menu = [
      ...(row.status === 'Pending' ? [{ key: 'start', label: 'Bắt đầu bảo trì', disabled: row.startDate > vietnamToday() }, { key: 'pause', label: 'Tạm dừng lịch' }] : []),
      ...(row.status === 'Paused' ? [{ key: 'resume', label: 'Tiếp tục lịch' }] : []),
      ...(editable ? [{ key: 'cancel', label: 'Hủy lịch', danger: true }] : []),
    ]
    return <div className="admin-action-group maintenance-row-actions">
      <Button className="admin-action-button admin-action-button--view" icon={<EyeOutlined />} aria-label={`Chi tiết ${row.taskName}`} onClick={() => setViewing(row)}>Chi tiết</Button>
      {editable && <Button className="admin-action-button admin-action-button--edit" disabled={mutation.isPending} icon={<EditOutlined />} aria-label={`Cập nhật ${row.taskName}`} onClick={() => edit(row)}>Cập nhật</Button>}
      {row.status === 'InProgress' && <Button type="primary" icon={<CheckCircleOutlined />} onClick={() => setCompleting(row)} disabled={mutation.isPending}>Hoàn thành</Button>}
      {menu.length > 0 && <Dropdown menu={{ items: menu, onClick: ({ key }) => changeState(row, key) }} trigger={['click']}><Button icon={<MoreOutlined />} aria-label={`Thao tác khác cho ${row.taskName}`} disabled={mutation.isPending} /></Dropdown>}
    </div>
  }

  const columns = [
    { title: 'Thiết bị / Công việc', key: 'equipment', width: '25%', render: (_, row) => <div className="maintenance-task-cell"><strong>{row.equipmentName}</strong><span>{row.taskName}</span>{row.location && <small>{row.location}</small>}</div> },
    { title: 'Chu kỳ', key: 'cycle', width: 115, render: (_, row) => recurrenceText(row) },
    { title: 'Ngày thực hiện', dataIndex: 'startDate', width: 125, render: value => dayjs(value).format('DD/MM/YYYY') },
    { title: 'Người phụ trách', dataIndex: 'assigneeName', width: '16%' },
    { title: 'Nhắc trước', dataIndex: 'remindBeforeDays', width: 100, render: value => value ? `${value} ngày` : 'Đến hạn' },
    { title: 'Trạng thái', key: 'status', width: 145, render: (_, row) => <Status schedule={row} /> },
    { title: 'Thao tác', key: 'actions', width: 240, render: (_, row) => actions(row) },
  ]

  const empty = <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Chưa có lịch bảo trì phù hợp"><Button type="primary" icon={<PlusOutlined />} onClick={() => { setEditing(null); setFormOpen(true) }}>Lập lịch bảo trì</Button></Empty>

  return <ConfigProvider theme={{ token: { colorPrimary: '#0077b6', colorInfo: '#0077b6', borderRadius: 8 } }}>
    <div className="equipment-list-page maintenance-page">
      {contextHolder}
      <header className="equipment-page-header">
        <div className="equipment-page-heading"><span className="equipment-header-icon" aria-hidden="true"><CalendarOutlined /></span><div><h1>Cấu hình lịch bảo trì</h1><p>Lập lịch, phân công và theo dõi bảo trì thiết bị hồ bơi.</p></div></div>
        <Button type="primary" size="large" icon={<PlusOutlined />} onClick={() => { setEditing(null); setFormOpen(true) }}>Lập lịch bảo trì</Button>
      </header>
      <div className="maintenance-summary">
        {[['Tổng lịch', summary?.total, <CalendarOutlined />, ''], ['Chờ thực hiện', summary?.pending, <ClockCircleOutlined />, ''], ['Đang thực hiện', summary?.inProgress, <CheckCircleOutlined />, 'maintenance-summary--progress'], ['Quá hạn', summary?.overdue, <ExclamationCircleOutlined />, 'maintenance-summary--overdue']].map(([label, value, icon, className]) => <div className={`maintenance-summary-card ${className}`} key={label}><span className="maintenance-summary-icon" aria-hidden="true">{icon}</span><div><span>{label}</span><strong>{list.isError || value == null ? '—' : value.toLocaleString('vi-VN')}</strong></div></div>)}
      </div>
      <section className="equipment-filter-panel" aria-label="Bộ lọc lịch bảo trì">
        <div className="equipment-panel-heading"><h2>Tìm kiếm & bộ lọc</h2><Button type="text" icon={<ReloadOutlined />} onClick={() => { setSearch(''); setParams({ pageIndex: 1, pageSize: params.pageSize, searchTerm: '', status: undefined }) }}>Đặt lại bộ lọc</Button></div>
        <div className="maintenance-toolbar">
          <label><span>Tìm kiếm</span><Input allowClear maxLength={255} prefix={<SearchOutlined />} placeholder="Tên thiết bị hoặc công việc..." value={search} onChange={event => setSearch(event.target.value)} /></label>
          <label><span>Thiết bị</span><Select allowClear showSearch optionFilterProp="label" aria-label="Lọc theo thiết bị" placeholder="Tất cả thiết bị" loading={options.isFetching} options={options.data?.equipments || []} value={params.equipmentId} onChange={value => changeFilter('equipmentId', value)} /></label>
          <label><span>Trạng thái</span><Select allowClear aria-label="Lọc trạng thái bảo trì" placeholder="Tất cả trạng thái" value={params.status} onChange={value => changeFilter('status', value)} options={[...Object.entries(maintenanceStatuses).map(([value, item]) => ({ value, label: item.label })), { value: 'Overdue', label: 'Quá hạn' }]} /></label>
        </div>
      </section>
      {options.isError && <Alert type="warning" showIcon title="Không thể tải danh sách thiết bị để lọc" action={<Button onClick={() => options.refetch()}>Thử lại</Button>} />}
      {mutation.isError && <Alert type="error" showIcon closable onClose={() => mutation.reset()} title={mutation.error?.response?.data?.message || 'Không thể cập nhật lịch bảo trì.'} />}
      <section className="equipment-list-panel" aria-label="Danh sách lịch bảo trì" aria-busy={busy}>
        <div className="equipment-panel-heading equipment-list-heading"><h2>Danh sách lịch bảo trì</h2><div className="maintenance-heading-actions"><Link to="/admin/equipments/maintenance-history"><Button icon={<HistoryOutlined />}>Lịch sử</Button></Link><Button type="text" icon={<ReloadOutlined spin={list.isFetching} />} disabled={busy} onClick={() => list.refetch()}>Làm mới</Button></div></div>
        {list.isError ? <div className="equipment-error"><Alert type="error" showIcon title={list.error?.response?.data?.message || 'Không thể tải lịch bảo trì.'} action={<Button onClick={() => list.refetch()}>Thử lại</Button>} /></div> : <>
          <Spin spinning={busy}>
            <Table className="maintenance-desktop-table" rowKey="id" columns={columns} dataSource={items} pagination={false} tableLayout="fixed" locale={{ emptyText: busy ? 'Đang tải lịch bảo trì...' : empty }} />
            <div className="maintenance-mobile-list">{items.map(row => <article className="maintenance-mobile-card" key={row.id}>
              <div className="maintenance-task-cell"><strong>{row.equipmentName}</strong><span>{row.taskName}</span></div><Status schedule={row} />
              <dl><div><dt>Ngày thực hiện</dt><dd>{dayjs(row.startDate).format('DD/MM/YYYY')}</dd></div><div><dt>Chu kỳ</dt><dd>{recurrenceText(row)}</dd></div><div><dt>Người phụ trách</dt><dd>{row.assigneeName}</dd></div><div><dt>Nhắc trước</dt><dd>{row.remindBeforeDays ? `${row.remindBeforeDays} ngày` : 'Đến hạn'}</dd></div></dl>
              {actions(row)}
            </article>)}{!items.length && (busy ? <div className="equipment-loading">Đang tải lịch bảo trì...</div> : empty)}</div>
          </Spin>
          <footer className="equipment-pagination"><Pagination current={params.pageIndex} pageSize={params.pageSize} total={page?.totalCount || 0} showSizeChanger pageSizeOptions={[10, 20, 50, 100]} locale={{ items_per_page: '/ trang' }}
            showTotal={(total, range) => total ? `${range[0]}–${range[1]} trong ${total} lịch` : 'Tổng 0 lịch'} onChange={(pageIndex, pageSize) => setParams(previous => ({ ...previous, pageIndex: pageSize === previous.pageSize ? pageIndex : 1, pageSize }))} /></footer>
        </>}
      </section>
      <MaintenanceFormModal open={formOpen} schedule={editing} onClose={() => { setFormOpen(false); setEditing(null) }} />
      <MaintenanceDetailsModal schedule={viewing && (items.find(row => row.id === viewing.id) || viewing)} onClose={() => setViewing(null)} onEdit={edit} />
      <CompleteMaintenanceModal schedule={completing} onClose={() => { setCompleting(null); changeFilter('pageIndex', 1) }} />
    </div>
  </ConfigProvider>
}
