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
  const register = useRegisterForClass(swimClass.id)
  const cancelRegistration = useCancelMyClassRegistration()
  const registration = registrationQuery.data
  const availability = classAvailability(swimClass, now)
  const canRegister = availability.kind === 'open' &&
    swimClass.createdByUserId !== userId &&
    !(swimClass.sessions || []).some(session =>
      session.status === 'Scheduled' && session.coachUserId === userId)
  const confirmed = registration?.registrationStatus === 'Confirmed'
  const pending = registration?.registrationStatus === 'PendingApproval'
  const rejected = registration?.registrationStatus === 'Rejected'
  const cancelled = registration?.registrationStatus === 'Cancelled'
  const canViewAttendance = swimClass.status === 'Active'
  const canSelfCancel = confirmed && !(swimClass.sessions || []).some(session =>
    session.status === 'Scheduled' && Date.parse(session.startsAt) <= now)

  function openAttendance() {
    navigate(`/lop-hoc-cua-toi/${swimClass.id}/diem-danh`,
      { state: { className: swimClass.name } })
  }

  function confirmCancel() {
    Modal.confirm({
      title: pending ? `Rút yêu cầu tham gia lớp ${swimClass.name}?` : `Hủy đăng ký lớp ${swimClass.name}?`,
      content: pending
        ? 'Coach sẽ không thể duyệt yêu cầu này sau khi bạn rút.'
        : 'Sau khi hủy, bạn sẽ không còn giữ chỗ trong lớp. Bạn có thể gửi yêu cầu lại nếu lớp vẫn nhận đăng ký và còn chỗ.',
      okText: pending ? 'Rút yêu cầu' : 'Xác nhận hủy',
      okType: 'danger',
      cancelText: 'Giữ đăng ký',
      onOk: async () => {
        try {
          await cancelRegistration.mutateAsync(swimClass.id)
          message.success(pending ? 'Đã rút yêu cầu tham gia lớp.' : 'Đã hủy đăng ký lớp.')
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
      message.success('Đã gửi yêu cầu tham gia lớp. Vui lòng chờ Coach duyệt.')
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
          {(canViewAttendance || canSelfCancel) && <div className="swim-class-registration-actions">
            {canViewAttendance && <Button type="primary" size="large" icon={<CameraOutlined />} onClick={openAttendance}>Xem điểm danh của tôi</Button>}
            {canSelfCancel && <Button danger icon={<StopOutlined />} loading={cancelRegistration.isPending}
              onClick={confirmCancel}>Hủy đăng ký</Button>}
          </div>}
          {!canSelfCancel && <p>Đã qua hạn tự hủy; vui lòng liên hệ Coach.</p>}
        </>
        : pending ? <>
          <Alert type="info" showIcon title="Yêu cầu đang chờ Coach duyệt"
            description="Yêu cầu chưa giữ chỗ trong lớp. Bạn có thể rút yêu cầu khi chưa được duyệt." />
          <Button danger icon={<StopOutlined />} loading={cancelRegistration.isPending}
            onClick={confirmCancel}>Rút yêu cầu</Button>
        </> : (cancelled || rejected) ? <>
          <Alert type="warning" showIcon title={rejected ? 'Yêu cầu đã bị từ chối' : 'Đăng ký trước đã bị hủy'}
            description={rejected && registration.reviewNote
              ? `Lý do: ${registration.reviewNote}`
              : cancelled && registration.cancelReason ? `Lý do: ${registration.cancelReason}` :
                canRegister ? 'Bạn có thể gửi lại nếu lớp vẫn còn chỗ.' : availability.text} />
          {((cancelled && canViewAttendance) || canRegister) && <div className="swim-class-registration-actions">
            {cancelled && canViewAttendance && <Button icon={<CameraOutlined />} onClick={openAttendance}>Xem điểm danh của tôi</Button>}
            {canRegister && <Button type="primary" icon={<UserAddOutlined />} loading={register.isPending} onClick={submit}>Gửi lại yêu cầu</Button>}
          </div>}
        </> : <>
          <p>{canRegister ? 'Gửi yêu cầu để Coach xem và duyệt. Yêu cầu chưa giữ chỗ trong lớp.' : availability.text}</p>
          {canRegister && <Button type="primary" icon={<UserAddOutlined />} loading={register.isPending} onClick={submit}>Gửi yêu cầu tham gia</Button>}
        </>}
      {submitError && <Alert className="swim-class-registration-error" type="error" showIcon title={submitError} />}
    </>}
  </section>
}
