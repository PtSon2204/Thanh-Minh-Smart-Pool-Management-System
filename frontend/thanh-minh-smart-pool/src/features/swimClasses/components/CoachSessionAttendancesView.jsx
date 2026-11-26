import { useEffect, useState } from 'react'
import { Alert, Button, Empty, Image, Modal, Pagination, Spin, Tag, message } from 'antd'
import { ArrowLeftOutlined, CameraOutlined, EditOutlined, ReloadOutlined } from '@ant-design/icons'
import { useLocation, useNavigate, useParams } from 'react-router-dom'
import { useAuthStore } from '../../auth/store/authStore'
import { useRecordClassAttendance, useSessionAttendances } from '../hooks/useClassAttendances'
import { attendanceStatus } from '../utils/classAttendance'
import { formatVietnamTime } from '../utils/classSchedule'
import { classServerMessage } from '../utils/classDisplay'
import CameraCaptureModal from '../../equipments/components/CameraCaptureModal'
import '../styles/classAttendance.css'

export default function CoachSessionAttendancesView() {
  const { classId, sessionId } = useParams()
  const navigate = useNavigate()
  const location = useLocation()
  const coachId = useAuthStore(state => state.session?.id)
  const [status, setStatus] = useState('all')
  const [pageIndex, setPageIndex] = useState(1)
  const [selected, setSelected] = useState(null)
  const [image, setImage] = useState(null)
  const [imagePreview, setImagePreview] = useState(null)
  const [cameraOpen, setCameraOpen] = useState(false)
  const [error, setError] = useState(null)
  const params = { pageIndex, pageSize: 10, ...(status !== 'all' ? { status } : {}) }
  const attendances = useSessionAttendances(classId, sessionId, coachId, params)
  const record = useRecordClassAttendance(classId, sessionId)
  const items = attendances.data?.items || []

  useEffect(() => {
    if (!image) return
    const url = URL.createObjectURL(image)
    setImagePreview(url)
    return () => URL.revokeObjectURL(url)
  }, [image])

  function close() {
    if (record.isPending) return
    setSelected(null); setImage(null); setImagePreview(null); setCameraOpen(false); setError(null)
  }
  function choose(item) {
    setSelected(item); setImage(null); setImagePreview(null); setError(null)
  }
  async function save(result) {
    if (!selected || record.isPending) return
    if (result === 'Verified' && !image && !selected.imageUrl) {
      setError('Vui lòng chụp ảnh học viên trước khi xác nhận có mặt.')
      return
    }
    setError(null)
    try {
      await record.mutateAsync({ registrationId: selected.registrationId, status: result,
        image: result === 'Verified' ? image : null, expectedUpdatedAt: selected.updatedAt })
      message.success(result === 'Verified' ? 'Đã xác nhận học viên có mặt.' : 'Đã đánh dấu học viên vắng mặt.')
      close()
    } catch (err) {
      setError(classServerMessage(err, 'Không lưu được điểm danh. Vui lòng thử lại.'))
      if (err?.response?.status === 409) void attendances.refetch()
    }
  }
  return <main className="class-attendance-page class-attendance-page--coach">
    <Button type="text" icon={<ArrowLeftOutlined />} onClick={() => navigate(`/coach/classes/${classId}`)}>Về chi tiết lớp</Button>
    <header className="class-attendance-header"><div><small>GÓC COACH</small><h1>Điểm danh buổi học</h1>
      {location.state?.startsAt && <p>{formatVietnamTime(location.state.startsAt)} – {formatVietnamTime(location.state.endsAt).slice(-5)}</p>}</div></header>
    <section className="class-attendance-panel" aria-label="Điểm danh của buổi học">
      <div className="class-attendance-toolbar">
        <select aria-label="Lọc trạng thái" value={status} onChange={e => { setStatus(e.target.value); setPageIndex(1) }}>
          <option value="all">Tất cả</option><option value="NotMarked">Chưa điểm danh</option>
          <option value="Verified">Có mặt</option><option value="Absent">Vắng mặt</option>
        </select>
        <Button icon={<ReloadOutlined />} onClick={() => attendances.refetch()}>Làm mới</Button>
      </div>
      {attendances.isPending && <Spin />}
      {attendances.isError && <Alert type="error" showIcon title="Không tải được danh sách học viên." />}
      {!attendances.isPending && !attendances.isError && !items.length && <Empty description="Không có học viên ở trạng thái này." />}
      <div className="class-attendance-list">{items.map(item => <article className="class-attendance-row" key={item.registrationId}>
        {item.imageUrl && <Image width={92} height={68} style={{ objectFit: 'cover' }} src={item.imageUrl} alt={`Ảnh điểm danh của ${item.customerName}`} />}
        <div className="class-attendance-row-main"><strong>{item.customerName}</strong>
          {item.submittedAt && <span>Ghi nhận lúc {formatVietnamTime(item.submittedAt)}</span>}</div>
        <Tag color={attendanceStatus[item.status]?.color || 'default'}>{attendanceStatus[item.status]?.label || item.status}</Tag>
        <Button icon={<EditOutlined />} disabled={location.state?.classStatus === 'Completed'} onClick={() => choose(item)}>Điểm danh / sửa</Button>
      </article>)}</div>
      {(attendances.data?.totalCount || 0) > 10 && <Pagination current={pageIndex} pageSize={10}
        total={attendances.data.totalCount} showSizeChanger={false} onChange={setPageIndex} />}
    </section>
    <Modal rootClassName="class-attendance-action-modal" title={`Điểm danh: ${selected?.customerName || ''}`} open={Boolean(selected)} onCancel={close}
      footer={<><Button onClick={close}>Đóng</Button><Button danger loading={record.isPending} disabled={Boolean(location.state?.endsAt && Date.parse(location.state.endsAt) >= Date.now())} onClick={() => save('Absent')}>Đánh dấu vắng</Button>
        <Button type="primary" loading={record.isPending} onClick={() => save('Verified')}>Xác nhận có mặt</Button></>}>
      <p>Coach có thể sửa điểm danh khi lớp còn hoạt động. Chỉ đánh dấu vắng sau buổi học.</p>
      <Button icon={<CameraOutlined />} disabled={record.isPending} onClick={() => setCameraOpen(true)}>Chụp ảnh học viên</Button>
      {image && <p>Ảnh mới: {image.name}</p>}
      {image && imagePreview && <Image width={160} src={imagePreview} alt="Ảnh mới chuẩn bị xác nhận" />}
      {!image && selected?.imageUrl && <Image width={160} src={selected.imageUrl} alt="Ảnh điểm danh hiện tại" />}
      {error && <Alert type="error" showIcon title={error} />}
    </Modal>
    {cameraOpen && <CameraCaptureModal open={cameraOpen} onClose={() => setCameraOpen(false)} mobileClassName="class-attendance-camera"
      onCapture={file => { if (!['image/jpeg','image/png'].includes(file.type) || file.size > 5 * 1024 * 1024) throw new Error('Ảnh phải là JPG/PNG tối đa 5 MB.'); setImage(file); setCameraOpen(false) }}
      title="Chụp ảnh học viên" maxPhotos={1} currentCount={0} captureHint="Chụp rõ học viên đang có mặt tại lớp" />}
  </main>
}
