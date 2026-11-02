import { useState } from 'react'
import { Alert, Button, Card, Drawer, Image, Input, Modal, Select, Space, Table, Tag, Typography, message } from 'antd'
import { CheckOutlined, CloseOutlined, EyeOutlined, ReloadOutlined, SafetyCertificateOutlined } from '@ant-design/icons'
import {
  useCoachApplicationDetail, useCoachApplicationList, useCoachCertificate,
  useReviewCoachApplication,
} from '../../../features/coachApplications/hooks/useCoachApplications'
import {
  coachApplicationStatus, formatCoachApplicationDate, getCoachApplicationError,
} from '../../../features/coachApplications/components/coachApplicationView'
import '../../../features/coachApplications/styles/coachApplications.css'

const { Title, Text } = Typography
const statusOptions = [
  { value: 'Pending', label: 'Đang chờ duyệt' },
  { value: 'Approved', label: 'Đã duyệt' },
  { value: 'Rejected', label: 'Đã từ chối' },
  { value: 'Revoked', label: 'Đã thu hồi quyền' },
]

export default function CoachApplicationsPage() {
  const [messageApi, contextHolder] = message.useMessage()
  const [status, setStatus] = useState(undefined)
  const [pageIndex, setPageIndex] = useState(1)
  const [pageSize, setPageSize] = useState(10)
  const [selectedId, setSelectedId] = useState(null)
  const [reviewDecision, setReviewDecision] = useState(null)
  const [reviewNote, setReviewNote] = useState('')
  const list = useCoachApplicationList({ status: status || undefined, pageIndex, pageSize })
  const detail = useCoachApplicationDetail(selectedId)
  const certificate = useCoachCertificate(selectedId)
  const review = useReviewCoachApplication()
  const item = detail.data

  function openReview(decision) {
    setReviewDecision(decision)
    setReviewNote('')
  }

  async function saveReview() {
    if (!selectedId || !reviewDecision) return
    if (reviewDecision === 'Rejected' && !reviewNote.trim()) {
      messageApi.error('Vui lòng nhập lý do từ chối.')
      return
    }
    try {
      await review.mutateAsync({ id: selectedId, decision: reviewDecision, note: reviewNote.trim() || null })
      messageApi.success(reviewDecision === 'Approved' ? 'Đã duyệt và cấp quyền Coach.' : 'Đã từ chối yêu cầu.')
      setReviewDecision(null)
    } catch (error) {
      messageApi.error(getCoachApplicationError(error))
    }
  }

  const columns = [
    { title: 'Người nộp', dataIndex: 'applicantName', key: 'applicantName',
      render: (value) => <strong>{value || 'Không có tên'}</strong> },
    { title: 'Ngày gửi', dataIndex: 'submittedAt', key: 'submittedAt',
      render: formatCoachApplicationDate },
    { title: 'Ngày xét duyệt', dataIndex: 'reviewedAt', key: 'reviewedAt',
      render: formatCoachApplicationDate },
    { title: 'Trạng thái', dataIndex: 'status', key: 'status',
      render: (value) => <Tag color={coachApplicationStatus[value]?.color}>{coachApplicationStatus[value]?.label || value}</Tag> },
    { title: 'Thao tác', key: 'actions',
      render: (_, record) => <Button icon={<EyeOutlined />} onClick={() => setSelectedId(record.id)}>Xem yêu cầu</Button> },
  ]

  return <div className="coach-app-admin">
    {contextHolder}
    <header className="coach-app-admin-hero">
      <div><span className="coach-app-eyebrow">QUẢN TRỊ HỆ THỐNG</span>
        <Title level={2}>Duyệt yêu cầu Coach</Title>
        <p>Xem chứng chỉ dạy bơi và quyết định cấp quyền cho người dùng.</p>
      </div>
      <SafetyCertificateOutlined />
    </header>

    <Card className="coach-app-card">
      <div className="coach-app-toolbar">
        <div><h2>Danh sách yêu cầu</h2><Text type="secondary">{list.data?.totalCount ?? 0} yêu cầu phù hợp</Text></div>
        <Space wrap>
          <Select aria-label="Lọc trạng thái yêu cầu" value={status} allowClear placeholder="Tất cả trạng thái"
            options={statusOptions} style={{ minWidth: 185 }} onChange={(value) => { setStatus(value); setPageIndex(1) }} />
          <Button icon={<ReloadOutlined />} onClick={() => list.refetch()}>Tải lại</Button>
        </Space>
      </div>
      {list.isError && <Alert type="error" showIcon message="Không thể tải danh sách yêu cầu"
        description={getCoachApplicationError(list.error)} action={<Button onClick={() => list.refetch()}>Thử lại</Button>}
        style={{ marginBottom: 16 }} />}
      <Table rowKey="id" columns={columns} dataSource={list.data?.items || []}
        loading={list.isLoading || list.isFetching} scroll={{ x: 650 }}
        pagination={{ current: pageIndex, pageSize, total: list.data?.totalCount || 0, showSizeChanger: true,
          pageSizeOptions: ['10', '20', '50'], onChange: (page, size) => {
            setPageIndex(size === pageSize ? page : 1); setPageSize(size)
          } }} />
    </Card>

    <Drawer title="Chi tiết yêu cầu Coach" open={Boolean(selectedId)} width={560}
      onClose={() => { setSelectedId(null); setReviewDecision(null) }}>
      {detail.isLoading ? <div className="coach-app-loading">Đang tải yêu cầu...</div> :
        detail.isError ? <Alert type="error" showIcon message="Không thể tải yêu cầu"
          action={<Button onClick={() => detail.refetch()}>Thử lại</Button>} /> : item ? <div className="coach-app-drawer">
          <Tag color={coachApplicationStatus[item.status]?.color} className="coach-app-status">
            {coachApplicationStatus[item.status]?.label || item.status}
          </Tag>
          <div className="coach-app-facts">
            <span>Người nộp</span><strong>{item.applicantName || '—'}</strong>
            <span>Ngày gửi</span><strong>{formatCoachApplicationDate(item.submittedAt)}</strong>
            {item.reviewedAt && <><span>Ngày xét duyệt</span><strong>{formatCoachApplicationDate(item.reviewedAt)}</strong></>}
            {item.revokedAt && <><span>Ngày thu hồi</span><strong>{formatCoachApplicationDate(item.revokedAt)}</strong></>}
          </div>
          {item.status !== 'Pending' && <Alert showIcon
            type={item.status === 'Rejected' ? 'error' : 'success'}
            message={item.status === 'Rejected' ? 'Lý do từ chối' : 'Kết quả duyệt'}
            description={item.reviewNote || (item.status !== 'Rejected'
              ? 'Yêu cầu đã được duyệt. Admin không để lại ghi chú.' : 'Không có ghi chú.')} />}
          {item.status === 'Revoked' && <Alert showIcon type="warning"
            message="Lý do thu hồi quyền Coach" description={item.revokeReason} style={{ marginTop: 16 }} />}
          <h3>Chứng chỉ dạy bơi</h3>
          {certificate.isLoading && <Text type="secondary">Đang tải ảnh...</Text>}
          {certificate.isError && <Alert showIcon type="error" message="Không thể xem chứng chỉ"
            action={<Button onClick={() => certificate.refetch()}>Thử lại</Button>} />}
          {certificate.data?.url && <Image className="coach-app-certificate" src={certificate.data.url}
            alt={`Chứng chỉ của ${item.applicantName || 'người nộp'}`} />}
          {item.status === 'Pending' && <div className="coach-app-review-actions">
            <Button danger icon={<CloseOutlined />} onClick={() => openReview('Rejected')}>Từ chối</Button>
            <Button type="primary" icon={<CheckOutlined />} onClick={() => openReview('Approved')}>Duyệt Coach</Button>
          </div>}
        </div> : <Alert type="warning" message="Không tìm thấy yêu cầu." />}
    </Drawer>

    <Modal title={reviewDecision === 'Approved' ? 'Duyệt yêu cầu Coach?' : 'Từ chối yêu cầu Coach?'}
      open={Boolean(reviewDecision)} okText={reviewDecision === 'Approved' ? 'Duyệt và cấp quyền' : 'Từ chối'}
      okButtonProps={{ danger: reviewDecision === 'Rejected', loading: review.isPending }}
      cancelText="Hủy" onCancel={() => { if (!review.isPending) setReviewDecision(null) }} onOk={saveReview}>
      <p>{reviewDecision === 'Approved'
        ? 'Người dùng sẽ có thêm quyền Coach và vẫn giữ quyền Customer.'
        : 'Người dùng có thể nộp yêu cầu mới sau khi bị từ chối.'}</p>
      <Input.TextArea aria-label="Ghi chú xét duyệt" placeholder={reviewDecision === 'Rejected' ? 'Lý do từ chối (bắt buộc)' : 'Ghi chú (không bắt buộc)'}
        value={reviewNote} maxLength={1000} showCount rows={4} onChange={(event) => setReviewNote(event.target.value)} />
    </Modal>
  </div>
}
