import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Button, Image, Modal, Popconfirm, Select, Space, Spin, Table, Tag, message } from 'antd'
import { monitoringService } from '../services/monitoringService'
import { canHandleAlert } from '../utils/alertActionWindow'
import { useAlertActionClock } from '../hooks/useAlertActionClock'

export default function AlertList({ onDetail }) {
  const [page, setPage] = useState(1)
  const [filters, setFilters] = useState({})
  const options = useQuery({ queryKey: ['safety', 'alert-filters'],
    queryFn: ({ signal }) => monitoringService.alertFilters(signal), refetchInterval: 30000 })
  const changeFilter = patch => { setFilters(current => ({ ...current, ...patch })); setPage(1) }
  const labels = { WARNING: 'Chú ý', CRITICAL: 'Khẩn cấp', EMERGENCY: 'Cứu hộ khẩn cấp',
    PENDING: 'Chờ xử lý', ACKNOWLEDGED: 'Đã tiếp nhận', CANCELLED: 'Đã hủy' }
  const choices = values => (values || []).map(value => ({ value, label: labels[value] || value }))
  const dates = (options.data?.dates || []).map(value => ({ value, label: value.split('-').reverse().join('/') }))
  const query = { ...filters, page, pageSize: 10,
    fromTime: filters.fromDate ? `${filters.fromDate}T00:00:00+07:00` : undefined,
    toTime: filters.toDate ? `${filters.toDate}T23:59:59.9999999+07:00` : undefined }
  delete query.fromDate
  delete query.toDate
  const [detailId, setDetailId] = useState(null)
  const [selectedId, setSelectedId] = useState(null)
  const now = useAlertActionClock(true)
  const detailQuery = useQuery({ queryKey: ['safety', 'alert', detailId], queryFn: ({ signal }) => monitoringService.alert(detailId, signal), enabled: !!detailId, refetchInterval: 2000 })
  const detail = detailQuery.data
  const client = useQueryClient()
  const alerts = useQuery({ queryKey: ['safety', 'alerts', query],
    queryFn: ({ signal }) => monitoringService.alerts(query, signal), refetchInterval: 5000 })
  const action = useMutation({ mutationFn: ({ alert, verb }) => {
    if (!canHandleAlert(alert)) {
      const error = new Error('Cảnh báo đã quá 10 giây, chỉ có thể xem chi tiết.')
      error.code = 'ALERT_ACTION_EXPIRED'
      throw error
    }
    return monitoringService.handleAlert(alert.id, verb, alert.severity)
  },
    onSuccess: (confirmed, { verb }) => {
      client.invalidateQueries({ queryKey: ['safety'] })
      if (verb === 'acknowledge') message.success(confirmed.incidentId ? 'Đã xác nhận và lưu vào lịch sử sự cố.' : 'Đã xác nhận cảnh báo.')
      setDetailId(null)
    },
    onError: error => { message.error(error.code === 'ALERT_ACTION_EXPIRED' ? error.message : error.response?.data?.message || 'Không xử lý được cảnh báo.'); client.invalidateQueries({ queryKey: ['safety'] }) } })
  return <>
    <Space wrap style={{ marginBottom: 16 }}>
      <Select aria-label="Khu vực cảnh báo" placeholder="Khu vực" allowClear showSearch optionFilterProp="label" style={{ minWidth: 160 }}
        loading={options.isPending} value={filters.area} options={choices(options.data?.areas)} onChange={area => changeFilter({ area })} />
      <Select aria-label="Mức độ cảnh báo" placeholder="Mức độ" allowClear style={{ minWidth: 150 }}
        loading={options.isPending} value={filters.severity} options={choices(options.data?.severities)} onChange={severity => changeFilter({ severity })} />
      <Select aria-label="Camera cảnh báo" placeholder="Camera" allowClear showSearch optionFilterProp="label" style={{ minWidth: 180 }}
        loading={options.isPending} value={filters.cameraId} options={(options.data?.cameras || []).map(camera => ({ value: camera.id, label: camera.name }))}
        onChange={cameraId => changeFilter({ cameraId })} />
      <Select aria-label="Trạng thái cảnh báo" placeholder="Trạng thái" allowClear style={{ minWidth: 160 }}
        loading={options.isPending} value={filters.status} options={choices(options.data?.statuses)} onChange={status => changeFilter({ status })} />
      <Select aria-label="Từ ngày" placeholder="Từ ngày" allowClear style={{ minWidth: 140 }} loading={options.isPending}
        value={filters.fromDate} options={dates.map(date => ({ ...date, disabled: !!filters.toDate && date.value > filters.toDate }))}
        onChange={fromDate => changeFilter({ fromDate })} />
      <Select aria-label="Đến ngày" placeholder="Đến ngày" allowClear style={{ minWidth: 140 }} loading={options.isPending}
        value={filters.toDate} options={dates.map(date => ({ ...date, disabled: !!filters.fromDate && date.value < filters.fromDate }))}
        onChange={toDate => changeFilter({ toDate })} />
      <Button onClick={() => { setFilters({}); setPage(1) }}>Đặt lại bộ lọc</Button>
    </Space>
    {options.isError && <Alert type="warning" title="Không tải được danh sách bộ lọc" action={<Button onClick={() => options.refetch()}>Thử lại</Button>} />}
    {alerts.isError && <Alert type="error" title="Không tải được cảnh báo" action={<Button onClick={() => alerts.refetch()}>Thử lại</Button>} />}
    <Table className="safety-alert-table" rowKey="id" size="small" loading={alerts.isPending} dataSource={alerts.data?.items || []}
      rowClassName={row => row.id === selectedId ? row.severity === 'CRITICAL' ? 'monitoring-alert-critical' : 'monitoring-alert-warning' : ''}
      scroll={{ x: 650 }} pagination={{ responsive: true, current: page, pageSize: 10, total: alerts.data?.totalCount || 0, onChange: setPage }} columns={[
        { title: 'Mức độ', render: (_, row) => <Tag color={['CRITICAL', 'EMERGENCY'].includes(row.severity) ? '#EF4444' : '#F97316'}>{['CRITICAL', 'EMERGENCY'].includes(row.severity) ? 'Khẩn cấp' : row.severity === 'WARNING' ? 'Chú ý' : row.severity}</Tag> },
        { title: 'Camera', render: (_, row) => row.cameraName || 'Camera' },
        { title: 'Thời gian', render: (_, row) => new Date(row.observedAt || row.createdAt).toLocaleString('vi-VN') },
        { title: 'Trạng thái', dataIndex: 'status' },
        { title: 'Thao tác', render: (_, row) => <Space wrap>
          <Button onClick={() => { setDetailId(row.id); setSelectedId(row.id); onDetail?.(row) }}>Xem chi tiết</Button>
          {canHandleAlert(row, now) && <>
            <Popconfirm title={row.severity === 'CRITICAL' ? 'Xác nhận và tạo sự cố cứu hộ?' : 'Xác nhận cảnh báo?'}
              onConfirm={() => action.mutate({ alert: row, verb: 'acknowledge' })}><Button loading={action.isPending}>Xác nhận</Button></Popconfirm>
            <Popconfirm title="Hủy cảnh báo này?" onConfirm={() => action.mutate({ alert: row, verb: 'cancel' })}><Button danger disabled={action.isPending}>Hủy</Button></Popconfirm>
          </>}
        </Space> },
      ]} />
    <Modal className="safety-modal" open={!!detailId} title="Ảnh chụp camera tại thời điểm cảnh báo" footer={null} onCancel={() => setDetailId(null)}>
      {detailQuery.isPending && <Spin />}
      {detailQuery.isError && <Alert type="error" title="Không tải được chi tiết cảnh báo" action={<Button onClick={() => detailQuery.refetch()}>Thử lại</Button>} />}
      {detail && <Space orientation="vertical" style={{ width: '100%' }}>
        {detail.imageUrl ? <Image src={detail.imageUrl} alt="Ảnh camera tại thời điểm phát hiện cảnh báo" width="100%" /> : <Alert type="warning" title="Cảnh báo cũ chưa có ảnh chụp." />}
        <span>{detail.cameraName || 'Camera'} · {detail.location} · Track {detail.trackId}</span>
        <span>{detail.severity} · {detail.status}</span>
        <span>Người xử lý: {detail.handledByName || (detail.handledBy ? 'Không có thông tin người xử lý' : 'Chưa xử lý')}</span>
        {detail.handledAt && <span>{new Date(detail.handledAt).toLocaleString('vi-VN')}</span>}
      </Space>}
    </Modal>
  </>
}
