import { useEffect, useState } from 'react'
import { Alert, Button, DatePicker, Form, Modal, Select, message } from 'antd'
import { useSwimClassCoaches, useUpdateClassSession } from '../hooks/useSwimClassManagement'
import { applyClassServerErrors, classServerMessage } from '../utils/classDisplay'
import { formatVietnamTime, utcTimeToVietnamPicker, vietnamPickerTimeToUtc } from '../utils/classSchedule'

const TIME_PICKER = { format: 'HH:mm', minuteStep: 15 }

export default function SwimClassSessionEditModal({ classId, session, onClose, onReload }) {
  const [form] = Form.useForm()
  const [error, setError] = useState(null)
  const [conflicts, setConflicts] = useState([])
  const update = useUpdateClassSession()
  const coachesQuery = useSwimClassCoaches(Boolean(session))
  const coachOptions = (coachesQuery.data || []).map(coach => ({
    value: coach.userId,
    label: coach.fullName || coach.username || coach.email || coach.userId,
  }))
  if (session && !coachOptions.some(item => item.value === session.coachUserId)) {
    coachOptions.unshift({ value: session.coachUserId, label: `${session.coachName || session.coachUserId} (không còn trong danh sách Coach đang làm việc)`, disabled: true })
  }

  useEffect(() => {
    if (!session) return
    form.setFieldsValue({
      startsAt: utcTimeToVietnamPicker(session.startsAt),
      endsAt: utcTimeToVietnamPicker(session.endsAt),
      coachUserId: session.coachUserId,
    })
  }, [session, form])

  async function submit(values) {
    setError(null)
    setConflicts([])
    try {
      await update.mutateAsync({
        classId,
        sessionId: session.id,
        payload: {
          startsAt: vietnamPickerTimeToUtc(values.startsAt),
          endsAt: vietnamPickerTimeToUtc(values.endsAt),
          coachUserId: values.coachUserId,
          expectedUpdatedAt: session.updatedAt,
        },
      })
      onClose()
      message.success('Đã cập nhật buổi học.')
    } catch (requestError) {
      applyClassServerErrors(form, requestError)
      setConflicts(requestError?.response?.data?.conflicts || [])
      setError(classServerMessage(requestError, 'Không thể cập nhật buổi học. Vui lòng thử lại.'))
    }
  }

  return <Modal title="Chỉnh sửa buổi học" open={Boolean(session)} onCancel={onClose} footer={null}
    width={560} destroyOnHidden maskClosable={!update.isPending} closable={!update.isPending}>
    {error && <Alert className="swim-class-form-alert" type="error" showIcon title={error}
      action={update.isError && update.error?.response?.status === 409 && conflicts.length === 0
        ? <Button size="small" onClick={() => { onClose(); onReload() }}>Tải lại dữ liệu</Button> : undefined} />}
    {conflicts.length > 0 && <Alert className="swim-class-form-alert" type="warning" showIcon title="Lịch bị trùng"
      description={<ul className="swim-class-conflict-list">{conflicts.map((item, index) =>
        <li key={`${item.conflictingSessionId || index}-${item.reason}`}>
          {item.reason === 'coach_overlap' ? 'Coach đã được phân công ở lớp khác' : 'Lớp đã có buổi học cùng giờ'}.
          {' '}Khung giờ yêu cầu: {formatVietnamTime(item.startsAt)}–{formatVietnamTime(item.endsAt)}
        </li>)}</ul>} />}
    {coachesQuery.isError && <Alert className="swim-class-form-alert" type="warning" showIcon
      title="Không tải được danh sách Coach" action={<Button size="small" onClick={() => coachesQuery.refetch()}>Thử lại</Button>} />}
    <Form form={form} layout="vertical" onFinish={submit} disabled={update.isPending}
      onValuesChange={() => { setError(null); setConflicts([]) }}>
      <div className="swim-class-form-grid">
        <Form.Item label="Bắt đầu" name="startsAt" rules={[
          { required: true, message: 'Chọn giờ bắt đầu.' },
          { validator: (_, value) => !value || Date.parse(vietnamPickerTimeToUtc(value)) > Date.now()
            ? Promise.resolve() : Promise.reject(new Error('Buổi học phải bắt đầu trong tương lai.')) },
        ]}><DatePicker className="swim-class-full-width" showTime={TIME_PICKER} format="DD/MM/YYYY HH:mm" /></Form.Item>
        <Form.Item label="Kết thúc" name="endsAt" dependencies={['startsAt']} rules={[
          { required: true, message: 'Chọn giờ kết thúc.' },
          ({ getFieldValue }) => ({ validator(_, value) {
            const start = getFieldValue('startsAt')
            return !value || !start || vietnamPickerTimeToUtc(value) > vietnamPickerTimeToUtc(start)
              ? Promise.resolve() : Promise.reject(new Error('Giờ kết thúc phải sau giờ bắt đầu.'))
          } }),
        ]}><DatePicker className="swim-class-full-width" showTime={TIME_PICKER} format="DD/MM/YYYY HH:mm" /></Form.Item>
        <Form.Item className="swim-class-span-full" label="Coach phụ trách" name="coachUserId" rules={[{ required: true, message: 'Chọn Coach phụ trách.' }]}>
          <Select showSearch optionFilterProp="label" loading={coachesQuery.isLoading} options={coachOptions} />
        </Form.Item>
      </div>
      <p className="swim-class-detail-timezone">Thời gian nhập theo giờ Việt Nam (UTC+7).</p>
      <div className="swim-class-edit-actions"><Button onClick={onClose} disabled={update.isPending}>Hủy</Button>
        <Button type="primary" htmlType="submit" loading={update.isPending} disabled={coachesQuery.isError}>Lưu thay đổi</Button></div>
    </Form>
  </Modal>
}
