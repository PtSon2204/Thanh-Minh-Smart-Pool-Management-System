import { useEffect, useState } from 'react'
import { Alert, Button, ConfigProvider, Empty, Image, Modal, Pagination, Spin, Tag, message } from 'antd'
import { ArrowLeftOutlined, CameraOutlined, ReloadOutlined } from '@ant-design/icons'
import { useLocation, useNavigate, useParams } from 'react-router-dom'
import { useAuthStore } from '../../auth/store/authStore'
import { useMyClassAttendances, useSubmitClassAttendance } from '../hooks/useClassAttendances'
import { attendanceStatus, canSubmitClassAttendance } from '../utils/classAttendance'
import { formatVietnamTime } from '../utils/classSchedule'
import { classServerMessage } from '../utils/classDisplay'
import CameraCaptureModal from '../../equipments/components/CameraCaptureModal'
import '../styles/classAttendance.css'

export default function MyClassAttendancesView() {
  const { classId } = useParams()
  const navigate = useNavigate()
  const location = useLocation()
  const userId = useAuthStore(state => state.session?.id)
  const [pageIndex, setPageIndex] = useState(1)
  const [now, setNow] = useState(() => Date.now())
  const [selected, setSelected] = useState(null)
  const [image, setImage] = useState(null)
  const [cameraOpen, setCameraOpen] = useState(false)
  const [error, setError] = useState(null)
  const attendances = useMyClassAttendances(classId, userId, pageIndex)
  const submit = useSubmitClassAttendance(userId, classId)

  useEffect(() => {
    const timer = setInterval(() => setNow(Date.now()), 30_000)
    return () => clearInterval(timer)
  }, [])

  function closeModal() {
    if (submit.isPending) return
    setSelected(null)
    setImage(null)
    setCameraOpen(false)
    setError(null)
  }

  function selectImage(file) {
    setError(null)
    if (!file) return false
    if (!['image/jpeg', 'image/png'].includes(file.type) || file.size < 4 || file.size > 5 * 1024 * 1024) {
      setError('Chỉ nhận ảnh JPG/PNG tối đa 5 MB.')
      return false
    }
    setImage(file)
    return true
  }

  function pickImage(event) {
    selectImage(event.target.files?.[0])
    event.target.value = ''
  }

  function captureImage(file) {
    if (!selectImage(file)) throw new Error('Ảnh chụp không hợp lệ hoặc vượt quá 5 MB.')
    setCameraOpen(false)
  }

  async function submitImage() {
    if (!selected || !image || submit.isPending) return
    setError(null)
    try {
      await submit.mutateAsync({ sessionId: selected.sessionId, image })
      message.success('Đã gửi ảnh điểm danh. Vui lòng chờ Coach duyệt.')
      closeModal()
    } catch (requestError) {
      setError(classServerMessage(requestError, 'Không gửi được ảnh điểm danh. Vui lòng thử lại.'))
      if (requestError?.response?.status === 409) void attendances.refetch()
    }
  }

  return <ConfigProvider theme={{ token: { colorPrimary: '#0077b6', colorInfo: '#0077b6', borderRadius: 8 } }}>
    <main className="class-attendance-page">
      <Button type="text" icon={<ArrowLeftOutlined />} onClick={() => navigate('/lop-hoc-cua-toi')}>
        Về lớp học của tôi
      </Button>
      <header className="class-attendance-header">
        <span className="class-attendance-header-icon"><CameraOutlined /></span>
        <div><small>GÓC HỌC VIÊN</small><h1>Điểm danh · {location.state?.className || 'Lớp học của tôi'}</h1>
          <p>Gửi ảnh từ khi buổi bắt đầu đến hết 24 giờ sau khi kết thúc.</p></div>
      </header>
      <section className="class-attendance-panel" aria-label="Điểm danh theo buổi">
        <div className="class-attendance-toolbar"><h2>Buổi học</h2>
          <Button type="text" icon={<ReloadOutlined spin={attendances.isFetching} />}
            disabled={attendances.isFetching} onClick={() => attendances.refetch()}>Làm mới</Button></div>
        {attendances.isPending && <div className="class-attendance-loading"><Spin /> Đang tải điểm danh...</div>}
        {attendances.isError && <Alert type="error" showIcon
          title={attendances.error?.response?.status === 404 ? 'Bạn chưa đăng ký lớp này.' : 'Không tải được điểm danh.'}
          action={<Button onClick={() => attendances.refetch()}>Thử lại</Button>} />}
        {!attendances.isPending && !attendances.isError && !attendances.data?.items?.length &&
          <Empty image={Empty.PRESENTED_IMAGE_SIMPLE} description="Lớp chưa có buổi học." />}
        {!attendances.isPending && !attendances.isError && <div className="class-attendance-list">
          {(attendances.data?.items || []).map(item => {
            const state = attendanceStatus[item.attendance?.status]
            const canSubmit = canSubmitClassAttendance(item, now)
            return <article className="class-attendance-row" key={item.sessionId}>
              <div className="class-attendance-row-main">
                <strong>{formatVietnamTime(item.startsAt)} – {formatVietnamTime(item.endsAt).slice(-5)}</strong>
                <span>{item.sessionStatus === 'Cancelled' ? 'Buổi đã hủy' : 'Giờ Việt Nam (UTC+7)'}</span>
                {item.attendance?.reviewNote && item.attendance.status === 'Rejected' &&
                  <p className="class-attendance-rejection">Lý do từ chối: {item.attendance.reviewNote}</p>}
              </div>
              {item.attendance ? <>
                <Tag color={state?.color || 'default'}>{state?.label || item.attendance.status}</Tag>
                <Image width={72} height={54} style={{ objectFit: 'cover', borderRadius: 6 }}
                  src={item.attendance.imageUrl} alt="Ảnh điểm danh đã gửi" />
              </> : canSubmit ? <Button type="primary" icon={<CameraOutlined />}
                onClick={() => { setSelected(item); setError(null) }}>Gửi ảnh điểm danh</Button>
                : <span className="class-attendance-muted">{item.sessionStatus === 'Cancelled' ? 'Không điểm danh' :
                  item.registrationStatus !== 'Confirmed' ? 'Đăng ký đã hủy' :
                    now < Date.parse(item.startsAt) ? 'Chưa đến giờ gửi ảnh' : 'Đã hết hạn gửi ảnh'}</span>}
            </article>
          })}
        </div>}
        {!attendances.isError && (attendances.data?.totalCount || 0) > 20 &&
          <Pagination className="class-attendance-pagination" current={pageIndex} pageSize={20}
            total={attendances.data.totalCount} showSizeChanger={false} onChange={setPageIndex} />}
      </section>
      <Modal title="Gửi ảnh điểm danh" open={Boolean(selected)} onCancel={closeModal}
        onOk={submitImage} okText="Gửi ảnh" okButtonProps={{ disabled: !image }}
        confirmLoading={submit.isPending} closable={!submit.isPending} maskClosable={!submit.isPending}
        destroyOnHidden>
        <p>Mỗi buổi chỉ được gửi ảnh một lần, kể cả khi Coach từ chối.</p>
        <Button icon={<CameraOutlined />} onClick={() => setCameraOpen(true)} disabled={submit.isPending}>
          Chụp ảnh trực tiếp
        </Button>
        <p className="class-attendance-muted">Hoặc chọn ảnh có sẵn:</p>
        <label className="class-attendance-file-picker">
          <input type="file" accept="image/jpeg,image/png" onChange={pickImage} disabled={submit.isPending} />
          {image ? image.name : 'Chọn ảnh JPG hoặc PNG, tối đa 5 MB'}
        </label>
        {error && <Alert type="error" showIcon title={error} />}
      </Modal>
      {cameraOpen && <CameraCaptureModal open={cameraOpen}
        onClose={() => setCameraOpen(false)} onCapture={captureImage}
        title="Chụp ảnh điểm danh" maxPhotos={1} currentCount={0}
        captureHint="Hướng camera vào vị trí cần chụp rồi bấm nút chụp" />}
    </main>
  </ConfigProvider>
}
