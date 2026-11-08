import { useState } from 'react'
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { Alert, Button, Image, Modal, Popconfirm, Space, Table, Tag, message } from 'antd'
import { monitoringService } from '../services/monitoringService'

export default function AlertList({ sourceType = 'LIVE', jobId, onDetail }) {
  const [page, setPage] = useState(1)
  const [detailId, setDetailId] = useState(null)
  const [selectedId, setSelectedId] = useState(null)
  const detailQuery = useQuery({ queryKey: ['safety', 'alert', detailId], queryFn: ({ signal }) => monitoringService.alert(detailId, signal), enabled: !!detailId, refetchInterval: 2000 })
  const detail = detailQuery.data
  const client = useQueryClient()
  const alerts = useQuery({ queryKey: ['safety', 'alerts', sourceType, jobId, page],
    queryFn: ({ signal }) => monitoringService.alerts({ sourceType, jobId, page, pageSize: 10 }, signal),
    enabled: sourceType === 'LIVE' || !!jobId, refetchInterval: 5000 })
  const action = useMutation({ mutationFn: ({ id, verb }) => monitoringService.handleAlert(id, verb),
    onSuccess: () => { client.invalidateQueries({ queryKey: ['safety'] }); setDetailId(null) },
    onError: error => { message.error(error.response?.data?.message || 'Không xử lý được cảnh báo.'); client.invalidateQueries({ queryKey: ['safety'] }) } })
  return <>
    {alerts.isError && <Alert type="error" title="Không tải được cảnh báo" action={<Button onClick={() => alerts.refetch()}>Thử lại</Button>} />}
    <Table rowKey="id" size="small" loading={alerts.isPending} dataSource={alerts.data?.items || []}
      rowClassName={row => row.id === selectedId ? row.severity === 'CRITICAL' ? 'monitoring-alert-critical' : 'monitoring-alert-warning' : ''}
      scroll={{ x: 750 }} pagination={{ current: page, pageSize: 10, total: alerts.data?.totalCount || 0, onChange: setPage }} columns={[
        { title: 'Nguồn', render: (_, row) => <Space><Tag>{row.sourceType}</Tag>{row.cameraName || 'Video tải lên'}</Space> },
        { title: 'Mức độ', render: (_, row) => <Tag color={['CRITICAL', 'EMERGENCY'].includes(row.severity) ? '#EF4444' : '#F97316'}>{['CRITICAL', 'EMERGENCY'].includes(row.severity) ? 'Khẩn cấp' : row.severity === 'WARNING' ? 'Chú ý' : row.severity}</Tag> },
        { title: 'Thời gian', render: (_, row) => row.sourceType === 'UPLOAD' ? `${row.videoTimestamp?.toFixed(1)}s` : new Date(row.createdAt).toLocaleString('vi-VN') },
        { title: 'Trạng thái', dataIndex: 'status' },
        { title: 'Thao tác', render: (_, row) => <Space wrap>
          <Button onClick={() => { setDetailId(row.id); setSelectedId(row.id); onDetail?.(row) }}>Xem chi tiết</Button>
          {row.status === 'PENDING' && <>
            <Popconfirm title={row.sourceType === 'LIVE' && row.severity === 'CRITICAL' ? 'Xác nhận và tạo sự cố cứu hộ?' : 'Xác nhận cảnh báo?'}
              onConfirm={() => action.mutate({ id: row.id, verb: 'acknowledge' })}><Button loading={action.isPending}>Xác nhận</Button></Popconfirm>
            <Popconfirm title="Hủy cảnh báo này?" onConfirm={() => action.mutate({ id: row.id, verb: 'cancel' })}><Button danger disabled={action.isPending}>Hủy</Button></Popconfirm>
          </>}
        </Space> },
      ]} />
    <Modal open={!!detailId} title="Chi tiết cảnh báo" footer={null} onCancel={() => setDetailId(null)}>
      {detailQuery.isError && <Alert type="error" title="Không tải được chi tiết cảnh báo" action={<Button onClick={() => detailQuery.refetch()}>Thử lại</Button>} />}
      {detail && <Space orientation="vertical">
        <span>{detail.cameraName || 'Video tải lên'} · {detail.location} · Track {detail.trackId}</span>
        <span>{detail.severity} · {detail.status}</span>
        <span>Người xử lý: {detail.handledBy || 'Chưa xử lý'}</span>
        {detail.handledAt && <span>{new Date(detail.handledAt).toLocaleString('vi-VN')}</span>}
        {detail.imageUrl ? <Image src={detail.imageUrl} alt="Ảnh sự cố" /> : <span>Chưa có hình ảnh sự cố</span>}
      </Space>}
    </Modal>
  </>
}
