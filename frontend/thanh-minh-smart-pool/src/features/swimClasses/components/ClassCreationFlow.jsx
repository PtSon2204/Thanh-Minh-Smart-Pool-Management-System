import { useState } from 'react'
import { CalendarOutlined, CheckCircleOutlined, TeamOutlined } from '@ant-design/icons'
import { ConfigProvider, Form, Steps, Tag, Typography } from 'antd'
import { useAuthStore } from '../../auth/store/authStore'
import { useActivateSwimClass, useCreateClassSessions, useCreateSwimClass, useSwimClassCoaches } from '../hooks/useSwimClassManagement'
import { applyClassServerErrors, classServerMessage } from '../utils/classDisplay'
import { clearClassDraft, readClassDraft, saveClassDraft } from '../utils/classDraftStorage'
import { findRequestedOverlap, vietnamPickerTimeToUtc } from '../utils/classSchedule'
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
    if (!draft || createSessions.isPending) return
    setSessionsError(null)
    setConflicts([])
    const overlap = findRequestedOverlap(values.sessions)
    if (overlap) {
      sessionsForm.setFields([{ name: ['sessions', overlap.index, 'startsAt'], errors: [`Trùng với buổi ${overlap.earlier + 1}.`] }])
      setSessionsError(`Buổi ${overlap.index + 1} trùng với buổi ${overlap.earlier + 1}.`)
      return
    }
    try {
      const saved = await createSessions.mutateAsync({
        classId: draft.id,
        sessions: values.sessions.map((item) => ({
          startsAt: vietnamPickerTimeToUtc(item.startsAt),
          endsAt: vietnamPickerTimeToUtc(item.endsAt),
          ...(item.coachUserId ? { coachUserId: item.coachUserId } : {}),
        })),
      })
      const next = { ...draft, sessions: [...draft.sessions, ...(saved.sessions || [])] }
      saveClassDraft(userId, next)
      setDraft(next)
      sessionsForm.resetFields()
      sessionsForm.setFieldsValue({ sessions: [{}] })
    } catch (error) {
      applyClassServerErrors(sessionsForm, error)
      setConflicts(error?.response?.data?.conflicts || [])
      setSessionsError(classServerMessage(error, 'Không thể lưu lịch học. Vui lòng thử lại.'))
    }
  }

  async function activateClass() {
    if (!draft || activate.isPending) return
    setActivationError(null)
    try {
      const activated = await activate.mutateAsync(draft.id)
      const next = { ...draft, ...activated }
      saveClassDraft(userId, next)
      setDraft(next)
    } catch (error) {
      setActivationError(classServerMessage(error, 'Không thể kích hoạt lớp. Vui lòng thử lại.'))
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
        <Tag color={stage === 2 ? 'success' : 'processing'}>{stage === 2 ? 'Đã kích hoạt' : draft ? 'Bản nháp đã lưu' : 'Lớp mới'}</Tag>
      </header>

      <section className="swim-class-step-panel" aria-label="Tiến độ tạo lớp">
        <Steps current={stage} responsive items={[
          { title: 'Thông tin lớp', icon: <TeamOutlined /> },
          { title: 'Lịch học', icon: <CalendarOutlined /> },
          { title: 'Hoàn tất', icon: <CheckCircleOutlined /> },
        ]} />
      </section>

      {stage === 0 && <SwimClassInfoForm form={classForm} onFinish={submitClass} pending={createClass.isPending}
        error={classError} />}

      {stage === 1 && <div className="swim-class-schedule-layout">
        <ClassDraftSummary draft={draft} coaches={coaches} />
        <div className="swim-class-schedule-main">
          <ClassSessionsEditor form={sessionsForm} onFinish={submitSessions} pending={createSessions.isPending}
            coachesQuery={coachesQuery} coachOptions={coachOptions} coaches={coaches} draft={draft}
            error={sessionsError} conflicts={conflicts}
            onValuesChange={() => { setConflicts([]); setSessionsError(null) }} />
          <ClassScheduleReview draft={draft} coaches={coaches} activationError={activationError}
            activating={activate.isPending} savingSessions={createSessions.isPending} onActivate={activateClass} />
        </div>
      </div>}

      {stage === 2 && <ClassCreationComplete draft={draft} onStartNew={startNewClass} />}
    </div>
  </ConfigProvider>
}
