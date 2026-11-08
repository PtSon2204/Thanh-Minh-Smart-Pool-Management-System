import { useState } from 'react'
import { Alert, Button, ConfigProvider, Empty, Form, Image, Input, Modal, Pagination, Segmented, Spin, Tag, message } from 'antd'
import { ArrowLeftOutlined, CheckCircleOutlined, ReloadOutlined, TeamOutlined, CloseCircleOutlined } from '@ant-design/icons'
import { useLocation, useNavigate, useParams } from 'react-router-dom'
import { useAuthStore } from '../../auth/store/authStore'
import { useReviewClassAttendance, useSessionAttendances } from '../hooks/useClassAttendances'
import { attendanceStatus } from '../utils/classAttendance'
import { formatVietnamTime } from '../utils/classSchedule'
import { applyClassServerErrors, classServerMessage } from '../utils/classDisplay'
import '../styles/classAttendance.css'

const filters = [
  { label: 'Tất cả', value: 'all' },
  { label: 'Chờ duyệt', value: 'PendingReview' },
  { label: 'Đã xác nhận', value: 'Verified' },
  { label: 'Bị từ chối', value: 'Rejected' },
]

export default function CoachSessionAttendancesView() {
  const { classId, sessionId } = useParams()
  const navigate = useNavigate()
  const location = useLocation()
  const coachId = useAuthStore(state => state.session?.id)
  const [status, setStatus] = useState('PendingReview')
  const [pageIndex, setPageIndex] = useState(1)
  const [selected, setSelected] = useState(null)
  const [decision, setDecision] = useState(null)
  const [error, setError] = useState(null)
  const [form] = Form.useForm()
  const params = { pageIndex, pageSize: 10, ...(status !== 'all' ? { status } : {}) }
  const attendances = useSessionAttendances(classId, sessionId, coachId, params)
  const review = useReviewClassAttendance(classId, sessionId)
  const items = attendances.data?.items || []

  function closeModal() {
    if (review.isPending) return
    setSelected(null)
    setDecision(null)
    setError(null)
    form.resetFields()
  }

  async function submit(values) {
    if (!selected || !decision) return
    setError(null)
    try {
      await review.mutateAsync({
        attendanceId: selected.attendanceId,
        payload: {
          decision,
          reviewNote: values.reviewNote?.trim() || null,
          expectedUpdatedAt: selected.updatedAt,
        },
      })
      message.success(decision === 'Verified' ? 'Đã xác nhận điểm danh.' : 'Đã từ chối điểm danh.')
      closeModal()
      if (status === 'PendingReview' && items.length === 1 && pageIndex > 1) setPageIndex(pageIndex - 1)
    } catch (requestError) {
      applyClassServerErrors(form, requestError)
      setError(classServerMessage(requestError, 'Không thể duyệt điểm danh. Vui lòng thử lại.'))
      if (requestError?.response?.status === 409) void attendances.refetch()
    }
  }

  return <ConfigProvider theme={{ token: { colorPrimary: '#0077b6', colorInfo: '#0077b6', borderRadius: 8 } }}>
    <main className="class-attendance-page class-attendance-page--coach">
      <Button type="text" icon={<ArrowLeftOutlined />} onClick={() => navigate(`/coach/classes/${classId}`)}>
        Về chi tiết lớp
      </Button>
      <header className="class-attendance-header">
        <span className="class-attendance-header-icon"><TeamOutlined /></span>
        <div><small>GÓC COACH</small><h1>Điểm danh buổi học</h1>
          {location.state?.startsAt && <p>{formatVietnamTime(location.state.startsAt)} – {formatVietnamTime(location.state.endsAt).slice(-5)}</p>}</div>
      </header>
      <section className="class-attendance-panel" aria-label="Điểm danh của buổi học">
        <div className="class-attendance-toolbar">
          <Segmented options={filters} value={status} onChange={value => { setStatus(value); setPageIndex(1) }}
            aria-label="Lọc trạng thái điểm danh" />
          <Button type="text" icon={<ReloadOutlined spin={attendances.isFetching} />}
            disabled={attendances.isFetching} onClick={() => attendances.refetch()}>Làm mới</Button>
        </div>
        {attendances.isPending && <div className="class-attendance-loading"><Spin /> Đang tải điểm danh...</div>}
        {attendances.isError && <Alert type={attendances.error?.response?.status === 403 ? 'warning' : 'error'} showIcon
          title={attendances.error?.response?.status === 403 ? 'Bạn không phụ trách buổi học này.' :
            attendances.error?.response?.status === 404 ? 'Không tìm thấy buổi học.' : 'Không tải được điểm danh.'}
          action={<Button onClick={() => attendances.refetch()}>Thử lại</Button>} />}
        {!attendances.isPending && !attendances.isError && items.length === 0 &&
          <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Không có ảnh điểm danh ở trạng thái này." />}
        {!attendances.isPending && !attendances.isError && <div className="class-attendance-list">
          {items.map(item => <article className="class-attendance-row" key={item.attendanceId}>
            <Image width={92} height={68} style={{ objectFit: 'cover', borderRadius: 6 }}
              src={item.imageUrl} alt={`Ảnh điểm danh của ${item.customerName}`} />
            <div className="class-attendance-row-main"><strong>{item.customerName}</strong>
              <span>Gửi lúc {formatVietnamTime(item.submittedAt)}</span>
              {item.status === 'Rejected' && item.reviewNote &&
                <p className="class-attendance-rejection">Lý do từ chối: {item.reviewNote}</p>}
            </div>
            <Tag color={attendanceStatus[item.status]?.color || 'default'}>
              {attendanceStatus[item.status]?.label || item.status}</Tag>
            {item.status === 'PendingReview' && <div className="class-attendance-actions">
              <Button type="primary" icon={<CheckCircleOutlined />}
                onClick={() => { setSelected(item); setDecision('Verified'); setError(null) }}>Xác nhận</Button>
              <Button danger icon={<CloseCircleOutlined />}
                onClick={() => { setSelected(item); setDecision('Rejected'); setError(null) }}>Từ chối</Button>
            </div>}
          </article>)}
        </div>}
        {!attendances.isError && (attendances.data?.totalCount || 0) > 10 &&
          <Pagination className="class-attendance-pagination" current={pageIndex} pageSize={10}
            total={attendances.data.totalCount} showSizeChanger={false} onChange={setPageIndex} />}
      </section>
      <Modal title={decision === 'Rejected' ? 'Từ chối điểm danh' : 'Xác nhận điểm danh'}
        open={Boolean(selected)} onCancel={closeModal} onOk={() => form.submit()}
        okText={decision === 'Rejected' ? 'Xác nhận từ chối' : 'Xác nhận điểm danh'}
        okType={decision === 'Rejected' ? 'danger' : 'primary'} confirmLoading={review.isPending}
        closable={!review.isPending} maskClosable={!review.isPending} destroyOnHidden>
        <p>Học viên: <strong>{selected?.customerName}</strong></p>
        {selected?.imageUrl && <Image width={160} src={selected.imageUrl} alt="Ảnh điểm danh cần duyệt" />}
        {error && <Alert type="error" showIcon title={error}
          action={review.error?.response?.status === 409
            ? <Button type="link" onClick={() => { closeModal(); attendances.refetch() }}>Tải lại danh sách</Button>
            : undefined} />}
        <Form form={form} layout="vertical" onFinish={submit} disabled={review.isPending}>
          <Form.Item name="reviewNote" label={decision === 'Rejected' ? 'Lý do từ chối' : 'Ghi chú (không bắt buộc)'}
            rules={[{ required: decision === 'Rejected', whitespace: true, message: 'Vui lòng nhập lý do từ chối.' },
              { max: 500, message: 'Ghi chú tối đa 500 ký tự.' }]}>
            <Input.TextArea rows={3} maxLength={500} showCount />
          </Form.Item>
        </Form>
      </Modal>
    </main>
  </ConfigProvider>
}
