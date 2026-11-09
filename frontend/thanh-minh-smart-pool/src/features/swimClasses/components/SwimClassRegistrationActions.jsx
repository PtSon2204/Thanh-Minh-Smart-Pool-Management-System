import { useState } from 'react'
import { Alert, Button, Modal, Spin, message } from 'antd'
import { CameraOutlined, ReloadOutlined, StopOutlined, UserAddOutlined } from '@ant-design/icons'
import { useNavigate } from 'react-router-dom'
import { classAvailability } from '../utils/classAvailability'
import { classServerMessage } from '../utils/classDisplay'
import { useCancelMyClassRegistration, useRegisterForClass } from '../hooks/useClassRegistration'

export default function SwimClassRegistrationActions({ swimClass, userId, now, registrationQuery, onRefresh }) {
  const navigate = useNavigate()
  const [submitError, setSubmitError] = useState(null)
  const register = useRegisterForClass(userId, swimClass.id)
  const cancelRegistration = useCancelMyClassRegistration()
  const registration = registrationQuery.data
  const availability = classAvailability(swimClass, now)
  const canRegister = availability.kind === 'open'
  const confirmed = registration?.registrationStatus === 'Confirmed'
  const cancelled = registration?.registrationStatus === 'Cancelled'
  const canSelfCancel = confirmed && !(swimClass.sessions || []).some(session =>
    session.status === 'Scheduled' && Date.parse(session.startsAt) <= now)

  function openAttendance() {
    navigate(`/lop-hoc-cua-toi/${swimClass.id}/diem-danh`,
      { state: { className: swimClass.name } })
  }

  function confirmCancel() {
    Modal.confirm({
      title: `Hủy đăng ký lớp ${swimClass.name}?`,
      content: 'Sau khi hủy, bạn sẽ không còn giữ chỗ trong lớp. Bạn có thể đăng ký lại nếu lớp vẫn nhận đăng ký và còn chỗ.',
      okText: 'Xác nhận hủy',
      okType: 'danger',
      cancelText: 'Giữ đăng ký',
      onOk: async () => {
        try {
          await cancelRegistration.mutateAsync(swimClass.id)
          message.success('Đã hủy đăng ký lớp.')
          onRefresh()
        } catch (error) {
          message.error(classServerMessage(error, 'Không thể hủy đăng ký. Vui lòng thử lại.'))
          void registrationQuery.refetch()
          onRefresh()
        }
      },
    })
  }

  async function submit() {
    if (register.isPending) return
    setSubmitError(null)
    try {
      await register.mutateAsync()
      message.success('Bạn đã đăng ký lớp thành công.')
    } catch (error) {
      setSubmitError(classServerMessage(error, 'Không thể đăng ký lớp. Vui lòng thử lại.'))
      if (error?.response?.status === 409) {
        void registrationQuery.refetch()
        onRefresh()
      }
    }
  }

  return <section className={`swim-class-detail-panel swim-class-registration-panel${confirmed ? ' swim-class-registration-panel--enrolled' : ''}`} aria-label="Đăng ký tham gia lớp">
    <div className="swim-class-detail-section-title"><UserAddOutlined /><h2>{confirmed ? 'Lớp học của tôi' : 'Tham gia lớp học'}</h2></div>
    {registrationQuery.isPending && <div className="swim-class-registration-loading"><Spin /> Đang kiểm tra đăng ký của bạn...</div>}
    {registrationQuery.isError && <Alert type="error" showIcon title="Không tải được trạng thái đăng ký"
      action={<Button icon={<ReloadOutlined />} loading={registrationQuery.isFetching} onClick={() => registrationQuery.refetch()}>Thử lại</Button>} />}
    {!registrationQuery.isPending && !registrationQuery.isError && <>
      {confirmed ? <>
          <div className="swim-class-registration-actions">
            <Button type="primary" size="large" icon={<CameraOutlined />} onClick={openAttendance}>Xem điểm danh của tôi</Button>
            {canSelfCancel && <Button danger icon={<StopOutlined />} loading={cancelRegistration.isPending}
              onClick={confirmCancel}>Hủy đăng ký</Button>}
          </div>
          {!canSelfCancel && <p>Đã qua hạn tự hủy; vui lòng liên hệ Coach.</p>}
        </>
        : cancelled ? <>
          <Alert type="warning" showIcon title="Đăng ký trước đã bị hủy"
            description={canRegister ? 'Bạn có thể đăng ký lại nếu lớp vẫn còn chỗ.' : availability.text} />
          <div className="swim-class-registration-actions">
            <Button icon={<CameraOutlined />} onClick={openAttendance}>Xem điểm danh của tôi</Button>
            {canRegister && <Button type="primary" icon={<UserAddOutlined />} loading={register.isPending} onClick={submit}>Đăng ký lại</Button>}
          </div>
        </> : <>
          <p>{canRegister ? 'Đăng ký lớp sẽ được xác nhận ngay.' : availability.text}</p>
          {canRegister && <Button type="primary" icon={<UserAddOutlined />} loading={register.isPending} onClick={submit}>Đăng ký lớp</Button>}
        </>}
      {submitError && <Alert className="swim-class-registration-error" type="error" showIcon title={submitError} />}
    </>}
  </section>
}
