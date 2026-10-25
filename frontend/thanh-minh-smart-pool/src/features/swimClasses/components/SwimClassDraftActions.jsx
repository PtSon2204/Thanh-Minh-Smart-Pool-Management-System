import { useState } from 'react'
import { Alert, Button, Form, message } from 'antd'
import { CalendarOutlined, CheckCircleOutlined } from '@ant-design/icons'
import { useActivateSwimClass, useCreateClassSessions, useSwimClassCoaches } from '../hooks/useSwimClassManagement'
import { applyClassServerErrors, classServerMessage } from '../utils/classDisplay'
import { findRequestedOverlap, vietnamPickerTimeToUtc } from '../utils/classSchedule'
import { canActivateClass } from '../utils/classAvailability'
import ClassSessionsEditor from './ClassSessionsEditor'

export default function SwimClassDraftActions({ swimClass, now }) {
  const [editorOpen, setEditorOpen] = useState(false)
  const [form] = Form.useForm()
  const [error, setError] = useState(null)
  const [activationError, setActivationError] = useState(null)
  const [conflicts, setConflicts] = useState([])
  const coachesQuery = useSwimClassCoaches(editorOpen)
  const coaches = coachesQuery.data || []
  const createSessions = useCreateClassSessions()
  const activate = useActivateSwimClass()
  const coachOptions = coaches.map(coach => ({
    value: coach.userId,
    label: coach.fullName || coach.username || coach.email || coach.userId,
  }))

  async function submitSessions(values) {
    if (createSessions.isPending) return
    setError(null)
    setConflicts([])
    const overlap = findRequestedOverlap(values.sessions)
    if (overlap) {
      form.setFields([{ name: ['sessions', overlap.index, 'startsAt'], errors: [`Trùng với buổi ${overlap.earlier + 1}.`] }])
      setError(`Buổi ${overlap.index + 1} trùng với buổi ${overlap.earlier + 1}.`)
      return
    }
    try {
      await createSessions.mutateAsync({
        classId: swimClass.id,
        sessions: values.sessions.map(session => ({
          startsAt: vietnamPickerTimeToUtc(session.startsAt),
          endsAt: vietnamPickerTimeToUtc(session.endsAt),
          ...(session.coachUserId ? { coachUserId: session.coachUserId } : {}),
        })),
      })
      form.resetFields()
      setEditorOpen(false)
      message.success('Đã lưu lịch học.')
    } catch (requestError) {
      applyClassServerErrors(form, requestError)
      setConflicts(requestError?.response?.data?.conflicts || [])
      setError(classServerMessage(requestError, 'Không thể lưu lịch học. Vui lòng thử lại.'))
    }
  }

  async function activateClass() {
    if (activate.isPending) return
    setActivationError(null)
    try {
      await activate.mutateAsync(swimClass.id)
      message.success('Lớp đã được kích hoạt.')
    } catch (requestError) {
      setActivationError(classServerMessage(requestError, 'Không thể kích hoạt lớp. Vui lòng thử lại.'))
    }
  }

  return <section className="swim-class-detail-panel swim-class-detail-actions" aria-label="Thao tác với lớp bản nháp">
    <div className="swim-class-detail-section-title"><CalendarOutlined /><h2>Hoàn thiện lớp</h2></div>
    <p>Thêm buổi học trước khi kích hoạt lớp để Customer có thể xem và đăng ký sau này.</p>
    {activationError && <Alert type="error" showIcon title={activationError} />}
    {coachesQuery.isError && editorOpen && <Alert type="warning" showIcon title="Không tải được danh sách Coach" action={<Button onClick={() => coachesQuery.refetch()}>Thử lại</Button>} />}
    <div className="swim-class-detail-action-buttons">
      <Button icon={<CalendarOutlined />} onClick={() => setEditorOpen(previous => !previous)}>{editorOpen ? 'Đóng biểu mẫu' : 'Thêm buổi học'}</Button>
      {canActivateClass(swimClass, now) && <Button type="primary" icon={<CheckCircleOutlined />} loading={activate.isPending} onClick={activateClass}>Kích hoạt lớp</Button>}
    </div>
    {editorOpen && <div className="swim-class-create-page swim-class-detail-session-editor">
      <ClassSessionsEditor form={form} onFinish={submitSessions} pending={createSessions.isPending}
        coachesQuery={coachesQuery} coachOptions={coachOptions} coaches={coaches} draft={swimClass}
        error={error} conflicts={conflicts} onValuesChange={() => { setError(null); setConflicts([]) }} />
    </div>}
  </section>
}
