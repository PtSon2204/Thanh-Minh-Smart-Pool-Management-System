import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Button, Image, Modal, Popconfirm, Space, Spin, Table, Tag, message } from 'antd'
import { monitoringService } from '../services/monitoringService'
import { canHandleAlert } from '../utils/alertActionWindow'
import { useAlertActionClock } from '../hooks/useAlertActionClock'

export default function AlertList({ sourceType = 'LIVE', jobId, onDetail }) {
  const [page, setPage] = useState(1)
  const [detailId, setDetailId] = useState(null)
  const [selectedId, setSelectedId] = useState(null)
  const now = useAlertActionClock(sourceType === 'LIVE')
  const detailQuery = useQuery({ queryKey: ['safety', 'alert', detailId], queryFn: ({ signal }) => monitoringService.alert(detailId, signal), enabled: !!detailId, refetchInterval: 2000 })
  const detail = detailQuery.data
  const client = useQueryClient()
  const alerts = useQuery({ queryKey: ['safety', 'alerts', sourceType, jobId, page],
    queryFn: ({ signal }) => monitoringService.alerts({ sourceType, jobId, page, pageSize: 10 }, signal),
    enabled: sourceType === 'LIVE' || !!jobId, refetchInterval: 5000 })
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
    {alerts.isError && <Alert type="error" title="Không tải được cảnh báo" action={<Button onClick={() => alerts.refetch()}>Thử lại</Button>} />}
    <Table className="safety-alert-table" rowKey="id" size="small" loading={alerts.isPending} dataSource={alerts.data?.items || []}
      rowClassName={row => row.id === selectedId ? row.severity === 'CRITICAL' ? 'monitoring-alert-critical' : 'monitoring-alert-warning' : ''}
      scroll={{ x: 750 }} pagination={{ responsive: true, current: page, pageSize: 10, total: alerts.data?.totalCount || 0, onChange: setPage }} columns={[
        { title: 'Nguồn', render: (_, row) => <Space><Tag>{row.sourceType}</Tag>{row.cameraName || 'Video tải lên'}</Space> },
        { title: 'Mức độ', render: (_, row) => <Tag color={['CRITICAL', 'EMERGENCY'].includes(row.severity) ? '#EF4444' : '#F97316'}>{['CRITICAL', 'EMERGENCY'].includes(row.severity) ? 'Khẩn cấp' : row.severity === 'WARNING' ? 'Chú ý' : row.severity}</Tag> },
        { title: 'Thời gian', render: (_, row) => row.sourceType === 'UPLOAD' ? `${row.videoTimestamp?.toFixed(1)}s` : new Date(row.observedAt || row.createdAt).toLocaleString('vi-VN') },
        { title: 'Trạng thái', dataIndex: 'status' },
        { title: 'Thao tác', render: (_, row) => <Space wrap>
          <Button onClick={() => { setDetailId(row.id); setSelectedId(row.id); onDetail?.(row) }}>Xem chi tiết</Button>
          {canHandleAlert(row, now) && <>
            <Popconfirm title={row.sourceType === 'LIVE' && row.severity === 'CRITICAL' ? 'Xác nhận và tạo sự cố cứu hộ?' : 'Xác nhận cảnh báo?'}
              onConfirm={() => action.mutate({ alert: row, verb: 'acknowledge' })}><Button loading={action.isPending}>Xác nhận</Button></Popconfirm>
            <Popconfirm title="Hủy cảnh báo này?" onConfirm={() => action.mutate({ alert: row, verb: 'cancel' })}><Button danger disabled={action.isPending}>Hủy</Button></Popconfirm>
          </>}
        </Space> },
      ]} />
    <Modal className="safety-modal" open={!!detailId} title={sourceType === 'LIVE' ? 'Ảnh chụp camera tại thời điểm cảnh báo' : 'Chi tiết cảnh báo'} footer={null} onCancel={() => setDetailId(null)}>
      {detailQuery.isPending && <Spin />}
      {detailQuery.isError && <Alert type="error" title="Không tải được chi tiết cảnh báo" action={<Button onClick={() => detailQuery.refetch()}>Thử lại</Button>} />}
      {detail && <Space orientation="vertical" style={{ width: '100%' }}>
        {detail.imageUrl ? <Image src={detail.imageUrl} alt="Ảnh camera tại thời điểm phát hiện cảnh báo" width="100%" /> : <Alert type="warning" title="Cảnh báo cũ chưa có ảnh chụp." />}
        <span>{detail.cameraName || 'Video tải lên'} · {detail.location} · Track {detail.trackId}</span>
        <span>{detail.severity} · {detail.status}</span>
        <span>Người xử lý: {detail.handledBy || 'Chưa xử lý'}</span>
        {detail.handledAt && <span>{new Date(detail.handledAt).toLocaleString('vi-VN')}</span>}
      </Space>}
    </Modal>
  </>
}
