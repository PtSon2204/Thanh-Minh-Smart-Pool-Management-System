import { useEffect, useState } from 'react'
import { Alert, Button, ConfigProvider, Descriptions, Empty, Input, Modal, Pagination, Select, Spin, Table } from 'antd'
import { CalendarOutlined, EyeOutlined, HistoryOutlined, ReloadOutlined, SearchOutlined } from '@ant-design/icons'
import { Link } from 'react-router-dom'
import dayjs from 'dayjs'
import { useMaintenanceHistory, useMaintenanceOptions } from "../../../features/maintenance/hooks/useMaintenance"
import "../../../features/services/styles/serviceManagement.css"
import "../../../features/equipments/styles/equipmentManagement.css"
import "../../../features/maintenance/styles/maintenance.css"

const money = value => new Intl.NumberFormat('vi-VN', { style: 'currency', currency: 'VND' }).format(value || 0)

export default function MaintenanceHistoryPage() {
  const [params, setParams] = useState({ pageIndex: 1, pageSize: 10, searchTerm: '' })
  const [search, setSearch] = useState('')
  const [viewing, setViewing] = useState(null)
  const history = useMaintenanceHistory(params)
  const options = useMaintenanceOptions()
  const items = history.data?.items || []

  useEffect(() => {
    const timer = setTimeout(() => setParams(previous => previous.searchTerm === search.trim() ? previous : { ...previous, searchTerm: search.trim(), pageIndex: 1 }), 350)
    return () => clearTimeout(timer)
  }, [search])

  const columns = [
    { title: 'Thiết bị / Công việc', key: 'task', render: (_, row) => <div className="maintenance-task-cell"><strong>{row.equipmentName}</strong><span>{row.taskName}</span></div> },
    { title: 'Thực hiện lúc', dataIndex: 'actualDate', width: 160, render: value => dayjs(value).format('DD/MM/YYYY HH:mm') },
    { title: 'Người thực hiện', dataIndex: 'performedBy', width: '18%' },
    { title: 'Chi phí thực tế', dataIndex: 'cost', width: 150, align: 'right', render: money },
    { title: 'Thao tác', key: 'actions', width: 120, render: (_, row) => <Button icon={<EyeOutlined />} onClick={() => setViewing(row)} aria-label={`Chi tiết kết quả ${row.taskName}`}>Chi tiết</Button> },
  ]
  const empty = <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Chưa có lịch sử bảo trì phù hợp" />

  return <ConfigProvider theme={{ token: { colorPrimary: '#0077b6', borderRadius: 8 } }}>
    <div className="equipment-list-page maintenance-page">
      <header className="equipment-page-header"><div className="equipment-page-heading"><span className="equipment-header-icon" aria-hidden="true"><HistoryOutlined /></span><div><h1>Lịch sử bảo trì</h1><p>Kết quả thực hiện và chi phí bảo trì thiết bị.</p></div></div><Link to="/admin/equipments/maintenance"><Button type="primary" size="large" icon={<CalendarOutlined />}>Lịch bảo trì</Button></Link></header>
      <section className="equipment-filter-panel" aria-label="Bộ lọc lịch sử bảo trì"><div className="maintenance-toolbar maintenance-toolbar--history">
        <label><span>Tìm kiếm</span><Input allowClear maxLength={255} prefix={<SearchOutlined />} placeholder="Tên thiết bị hoặc công việc..." value={search} onChange={event => setSearch(event.target.value)} /></label>
        <label><span>Thiết bị</span><Select allowClear showSearch optionFilterProp="label" aria-label="Lọc thiết bị trong lịch sử" placeholder="Tất cả thiết bị" value={params.equipmentId} options={options.data?.equipments || []} loading={options.isFetching} onChange={equipmentId => setParams(previous => ({ ...previous, equipmentId, pageIndex: 1 }))} /></label>
      </div></section>
      {options.isError && <Alert type="warning" showIcon title="Không thể tải danh sách thiết bị để lọc" action={<Button onClick={() => options.refetch()}>Thử lại</Button>} />}
      <section className="equipment-list-panel" aria-label="Lịch sử bảo trì thiết bị">
        <div className="equipment-panel-heading equipment-list-heading"><h2>Kết quả bảo trì</h2><Button type="text" icon={<ReloadOutlined spin={history.isFetching} />} disabled={history.isFetching} onClick={() => history.refetch()}>Làm mới</Button></div>
        {history.isError ? <div className="equipment-error"><Alert type="error" showIcon title={history.error?.response?.data?.message || 'Không thể tải lịch sử bảo trì.'} action={<Button onClick={() => history.refetch()}>Thử lại</Button>} /></div> : <>
          <Spin spinning={history.isFetching || search.trim() !== params.searchTerm}>
            <Table className="maintenance-desktop-table" rowKey="id" columns={columns} dataSource={items} pagination={false} tableLayout="fixed" locale={{ emptyText: history.isFetching ? 'Đang tải lịch sử...' : empty }} />
            <div className="maintenance-mobile-list">{items.map(row => <article className="maintenance-mobile-card" key={row.id}><div className="maintenance-task-cell"><strong>{row.equipmentName}</strong><span>{row.taskName}</span></div><dl><div><dt>Thực hiện lúc</dt><dd>{dayjs(row.actualDate).format('DD/MM/YYYY HH:mm')}</dd></div><div><dt>Người thực hiện</dt><dd>{row.performedBy}</dd></div><div><dt>Chi phí thực tế</dt><dd>{money(row.cost)}</dd></div></dl><Button icon={<EyeOutlined />} onClick={() => setViewing(row)}>Chi tiết kết quả</Button></article>)}{!items.length && (history.isFetching ? <div className="equipment-loading">Đang tải lịch sử...</div> : empty)}</div>
          </Spin>
          <footer className="equipment-pagination"><Pagination current={params.pageIndex} pageSize={params.pageSize} total={history.data?.totalCount || 0} showSizeChanger locale={{ items_per_page: '/ trang' }} pageSizeOptions={[10, 20, 50, 100]} showTotal={total => `Tổng ${total} kết quả`} onChange={(pageIndex, pageSize) => setParams(previous => ({ ...previous, pageIndex: pageSize === previous.pageSize ? pageIndex : 1, pageSize }))} /></footer>
        </>}
      </section>
      <Modal title="Kết quả bảo trì" open={Boolean(viewing)} onCancel={() => setViewing(null)} className="maintenance-modal" width={640} footer={<Button onClick={() => setViewing(null)}>Đóng</Button>}>
        {viewing && <Descriptions bordered column={1} size="small" items={[
          { key: 'equipment', label: 'Thiết bị', children: viewing.equipmentName },
          { key: 'task', label: 'Công việc', children: viewing.taskName },
          { key: 'planned', label: 'Ngày dự kiến', children: viewing.plannedDate ? dayjs(viewing.plannedDate).format('DD/MM/YYYY') : 'Không có lịch trước' },
          { key: 'actual', label: 'Thực hiện lúc', children: dayjs(viewing.actualDate).format('DD/MM/YYYY HH:mm') },
          { key: 'actor', label: 'Người ghi nhận', children: viewing.performedBy },
          { key: 'vendor', label: 'Đơn vị bảo trì', children: viewing.vendorName || 'Nội bộ' },
          { key: 'cost', label: 'Chi phí', children: money(viewing.cost) },
          { key: 'note', label: 'Kết quả', children: <span className="maintenance-result-note">{viewing.note || 'Chưa có ghi chú'}</span> },
        ]} />}
      </Modal>
    </div>
  </ConfigProvider>
}
