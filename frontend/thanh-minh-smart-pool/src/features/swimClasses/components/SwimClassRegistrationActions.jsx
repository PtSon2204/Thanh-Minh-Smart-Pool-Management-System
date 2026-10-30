import { useState } from 'react'
import { Alert, Button, Spin, message } from 'antd'
import { ReloadOutlined, UserAddOutlined } from '@ant-design/icons'
import { classAvailability } from '../utils/classAvailability'
import { classServerMessage } from '../utils/classDisplay'
import { useMyClassRegistration, useRegisterForClass } from '../hooks/useClassRegistration'

export default function SwimClassRegistrationActions({ swimClass, userId, now, onRefresh }) {
  const [submitError, setSubmitError] = useState(null)
  const registrationQuery = useMyClassRegistration(swimClass.id, userId)
  const register = useRegisterForClass(userId, swimClass.id)
  const registration = registrationQuery.data
  const availability = classAvailability(swimClass, now)
  const canRegister = availability.kind === 'open'
  const confirmed = registration?.registrationStatus === 'Confirmed'
  const cancelled = registration?.registrationStatus === 'Cancelled'

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

  return <section className="swim-class-detail-panel swim-class-registration-panel" aria-label="Đăng ký tham gia lớp">
    <div className="swim-class-detail-section-title"><UserAddOutlined /><h2>Tham gia lớp học</h2></div>
    {registrationQuery.isPending && <div className="swim-class-registration-loading"><Spin /> Đang kiểm tra đăng ký của bạn...</div>}
    {registrationQuery.isError && <Alert type="error" showIcon title="Không tải được trạng thái đăng ký"
      action={<Button icon={<ReloadOutlined />} loading={registrationQuery.isFetching} onClick={() => registrationQuery.refetch()}>Thử lại</Button>} />}
    {!registrationQuery.isPending && !registrationQuery.isError && <>
      {confirmed ? <Alert type="success" showIcon title="Bạn đã đăng ký lớp thành công"
        description="Đăng ký đã được xác nhận. Lịch học của bạn ở thời khóa biểu phía dưới." />
        : cancelled ? <>
          <Alert type="warning" showIcon title="Đăng ký trước đã bị hủy"
            description={canRegister ? 'Bạn có thể đăng ký lại nếu lớp vẫn còn chỗ.' : availability.text} />
          {canRegister && <Button type="primary" icon={<UserAddOutlined />} loading={register.isPending} onClick={submit}>Đăng ký lại</Button>}
        </> : <>
          <p>{canRegister ? 'Đăng ký lớp sẽ được xác nhận ngay.' : availability.text}</p>
          {canRegister && <Button type="primary" icon={<UserAddOutlined />} loading={register.isPending} onClick={submit}>Đăng ký lớp</Button>}
        </>}
      {submitError && <Alert className="swim-class-registration-error" type="error" showIcon title={submitError} />}
    </>}
  </section>
}
