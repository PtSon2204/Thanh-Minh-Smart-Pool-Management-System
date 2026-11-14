import { useState } from 'react'
import { CalendarOutlined, CheckCircleOutlined, EyeOutlined, PlusOutlined, TeamOutlined } from '@ant-design/icons'
import { Button, ConfigProvider, Form, Popconfirm, Steps, Tag, Typography } from 'antd'
import { useAuthStore } from '../../auth/store/authStore'
import { useActivateSwimClass, useCreateClassSessions, useCreateSwimClass, useSwimClassCoaches } from '../hooks/useSwimClassManagement'
import { applyClassServerErrors, classServerMessage } from '../utils/classDisplay'
import { clearClassDraft, readClassDraft, saveClassDraft } from '../utils/classDraftStorage'
import { findRequestedOverlap, utcTimeToVietnamPicker, vietnamPickerTimeToUtc } from '../utils/classSchedule'
import ClassCreationComplete from './ClassCreationComplete'
import ClassDraftSummary from './ClassDraftSummary'
import ClassScheduleReview from './ClassScheduleReview'
import ClassSessionsEditor from './ClassSessionsEditor'
import SwimClassInfoForm from './SwimClassInfoForm'
import '../styles/swimClassManagement.css'

const { Text, Title } = Typography

export default function ClassCreationFlow() {
  const session = useAuthStore((state) => state.session)
  const userId = session?.id
  const [classForm] = Form.useForm()
  const [sessionsForm] = Form.useForm()
  const [draft, setDraft] = useState(() => readClassDraft(userId))
  const [classError, setClassError] = useState(null)
  const [sessionsError, setSessionsError] = useState(null)
  const [activationError, setActivationError] = useState(null)
  const [conflicts, setConflicts] = useState([])
  const stage = draft?.status === 'Active' ? 2 : draft ? 1 : 0

  const coachesQuery = useSwimClassCoaches(Boolean(userId && stage !== 2))
  const coaches = coachesQuery.data || []
  const coachOptions = coaches.map((coach) => ({
    value: coach.userId,
    label: coach.fullName || coach.username || coach.email || coach.userId,
  }))
  const createClass = useCreateSwimClass()
  const createSessions = useCreateClassSessions()
  const activate = useActivateSwimClass()

  async function submitClass(values) {
    if (createClass.isPending) return
    setClassError(null)
    try {
      const created = await createClass.mutateAsync({
        name: values.name.trim(),
        description: values.description?.trim() || null,
        capacity: values.capacity,
        fee: values.fee,
        registrationStartAt: vietnamPickerTimeToUtc(values.registrationStartAt),
        registrationEndAt: vietnamPickerTimeToUtc(values.registrationEndAt),
      })
      const next = { ...created, sessions: [] }
      saveClassDraft(userId, next)
      setDraft(next)
      sessionsForm.setFieldsValue({ sessions: [{}] })
    } catch (error) {
      applyClassServerErrors(classForm, error)
      setClassError(classServerMessage(error, 'Không thể tạo lớp. Vui lòng thử lại.'))
    }
  }

  async function submitSessions(values) {
    if (!draft || createSessions.isPending || activate.isPending) return
    setSessionsError(null)
    setConflicts([])
    const overlap = findRequestedOverlap([
      ...draft.sessions.filter(item => item.status === 'Scheduled').map(item => ({
        startsAt: utcTimeToVietnamPicker(item.startsAt),
        endsAt: utcTimeToVietnamPicker(item.endsAt),
      })),
      ...values.sessions,
    ])
    if (overlap) {
      const savedCount = draft.sessions.filter(item => item.status === 'Scheduled').length
      const formIndex = overlap.index - savedCount
      if (formIndex >= 0) sessionsForm.setFields([{ name: ['sessions', formIndex, 'startsAt'], errors: ['Trùng với buổi đã lưu.'] }])
      setSessionsError('Buổi học bị trùng với lịch đã lưu hoặc buổi khác đang nhập.')
      return
    }
    const next = { ...draft, sessions: [...draft.sessions,
      ...values.sessions.map(item => ({
        id: crypto.randomUUID(),
        isLocal: true,
        status: 'Scheduled',
        coachUserId: userId,
        startsAt: vietnamPickerTimeToUtc(item.startsAt),
        endsAt: vietnamPickerTimeToUtc(item.endsAt),
      })),
    ] }
    saveClassDraft(userId, next)
    setDraft(next)
    sessionsForm.resetFields()
    sessionsForm.setFieldsValue({ sessions: [{}] })
  }

  function removeSession(sessionId) {
    if (!draft || createSessions.isPending || activate.isPending) return
    const next = { ...draft, sessions: draft.sessions.filter(item => item.id !== sessionId) }
    saveClassDraft(userId, next)
    setDraft(next)
    setActivationError(null)
  }

  async function activateClass() {
    if (!draft || activate.isPending || createSessions.isPending) return
    setActivationError(null)
    try {
      let ready = draft
      const localSessions = draft.sessions.filter(item => item.isLocal)
      if (localSessions.length > 0) {
        const saved = await createSessions.mutateAsync({
          classId: draft.id,
          sessions: localSessions.map(({ startsAt, endsAt }) => ({ startsAt, endsAt })),
        })
        ready = { ...draft, sessions: [
          ...draft.sessions.filter(item => !item.isLocal),
          ...(saved.sessions || []),
        ] }
        saveClassDraft(userId, ready)
        setDraft(ready)
      }
      const activated = await activate.mutateAsync(draft.id)
      const next = { ...ready, ...activated }
      saveClassDraft(userId, next)
      setDraft(next)
    } catch (error) {
      setActivationError(classServerMessage(error, 'Không thể lưu lịch và kích hoạt lớp. Vui lòng thử lại.'))
    }
  }

  function startNewClass() {
    clearClassDraft(userId)
    classForm.resetFields()
    sessionsForm.resetFields()
    setDraft(null)
    setClassError(null)
    setSessionsError(null)
    setActivationError(null)
    setConflicts([])
  }

  return <ConfigProvider theme={{ token: { colorPrimary: '#0077b6', colorInfo: '#0077b6', borderRadius: 8 } }}>
    <div className="swim-class-create-page swim-class-create-page--coach">
      <header className="swim-class-page-header">
        <div className="swim-class-page-title">
          <span className="swim-class-page-icon"><TeamOutlined /></span>
          <div><span className="swim-class-eyebrow">KHU VỰC COACH</span><Title level={2}>Tạo lớp và lịch học</Title>
            <Text type="secondary">Thiết lập lớp của bạn và sắp xếp các buổi học.</Text></div>
        </div>
        <div className="swim-class-page-header-actions">
          {stage === 1 && <Popconfirm
            title="Tạo lớp mới?"
            description="Bản nháp hiện tại vẫn được lưu và có thể mở lại từ danh sách lớp."
            okText="Tạo lớp mới" cancelText="Ở lại" onConfirm={startNewClass}>
            <Button icon={<PlusOutlined />}>Tạo lớp khác</Button>
          </Popconfirm>}
          <Tag color={stage === 2 ? 'success' : 'processing'}>{stage === 2 ? 'Đã kích hoạt' : draft ? 'Bản nháp đã lưu' : 'Lớp mới'}</Tag>
        </div>
      </header>

      <section className="swim-class-step-panel" aria-label="Tiến độ tạo lớp">
        <Steps current={stage} responsive items={[
          { title: 'Thông tin lớp', icon: <TeamOutlined /> },
          { title: 'Lịch học', icon: <CalendarOutlined /> },
          { title: 'Hoàn tất', icon: <CheckCircleOutlined /> },
        ]} />
      </section>

      <div className="swim-class-flow-next" role="status">
        <div><strong>{stage === 0 ? '1. Nhập thông tin lớp' : stage === 1 ? '2. Sắp lịch và mở đăng ký' : '3. Xem và quản lý lớp'}</strong>
          <span>{stage === 0 ? 'Lưu thông tin lớp trước khi thêm các buổi học.' : stage === 1 ? 'Thêm và chỉnh lịch trong bản nháp. Các buổi học chỉ được gửi khi bạn kích hoạt lớp.' : 'Lớp đã sẵn sàng. Mở trang lớp để xem lịch và chỉnh sửa từng buổi.'}</span></div>
        {draft && <Button icon={<EyeOutlined />} href={`/coach/classes/${draft.id}`}>Xem lớp {stage === 1 ? 'nháp' : ''}</Button>}
      </div>

      {stage === 0 && <SwimClassInfoForm form={classForm} onFinish={submitClass} pending={createClass.isPending}
        error={classError} />}

      {stage === 1 && <div className="swim-class-schedule-layout">
        <ClassDraftSummary draft={draft} coaches={coaches} />
        <div className="swim-class-schedule-main">
          <ClassSessionsEditor form={sessionsForm} onFinish={submitSessions} pending={createSessions.isPending || activate.isPending}
            coachesQuery={coachesQuery} coachOptions={coachOptions} coaches={coaches} draft={draft}
            error={sessionsError} conflicts={conflicts}
            onValuesChange={() => { setConflicts([]); setSessionsError(null) }} />
          <ClassScheduleReview draft={draft} coaches={coaches} activationError={activationError}
            activating={activate.isPending || createSessions.isPending} savingSessions={createSessions.isPending}
            onActivate={activateClass} onRemoveSession={removeSession} />
        </div>
      </div>}

      {stage === 2 && <ClassCreationComplete draft={draft} onStartNew={startNewClass} />}
    </div>
  </ConfigProvider>
}
