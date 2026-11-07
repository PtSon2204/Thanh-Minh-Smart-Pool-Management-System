import { useCallback, useEffect, useRef, useState } from 'react'
import { Alert, Button, ConfigProvider, Input, Space, Spin, Typography } from 'antd'
import { Link } from 'react-router-dom'
import { useAuthStore } from '../../features/auth/store/authStore'
import OnboardingProfileForm from '../../features/staff/components/OnboardingProfileForm'
import OnboardingDocuments from '../../features/staff/components/OnboardingDocuments'
import OnboardingRevisionDetails from '../../features/staff/components/OnboardingRevisionDetails'
import StaffContractSchedule from '../../features/staff/components/StaffContractSchedule'
import { onboardingService } from '../../features/staff/services/onboardingService'
import { onboardingError, stateLabels } from '../../features/staff/onboarding/onboardingUi'
import '../../features/staff/styles/staffOnboarding.css'

const onboardingTheme = { token: { colorPrimary: '#0077b6', colorInfo: '#0077b6', fontFamily: 'var(--sans)' } }

// Module memory survives StrictMode initialization, but never survives a page refresh.
let linkCredentials
function takeLink() {
  const fragment = new URLSearchParams(window.location.hash.slice(1))
  if (fragment.has('invitation') || fragment.has('token')) {
    linkCredentials = { invitationId: fragment.get('invitation'), token: fragment.get('token') }
    window.history.replaceState(null, '', window.location.pathname + window.location.search)
  }
  return linkCredentials
}

export default function StaffOnboardingPage() {
  const [credentials, setCredentials] = useState(takeLink)
  const session = useAuthStore((state) => state.session)
  const staffSession = ['ADMIN', 'SALES', 'LIFEGUARD', 'COACH'].includes(session?.role?.toUpperCase())
  const [workspace, setWorkspace] = useState(null)
  const [error, setError] = useState('')
  const [busy, setBusy] = useState(false)
  const [loading, setLoading] = useState(true)
  const [reason, setReason] = useState('')
  const guard = useRef(false)
  const [uncertain, setUncertain] = useState(false)

  const reload = useCallback(async () => {
    setLoading(true)
    setError('')
    try {
      const data = credentials ? await onboardingService.inspect(credentials) : await onboardingService.own()
      setWorkspace(data)
      setUncertain(false)
    } catch (failure) { setError(onboardingError(failure)) }
    finally { setLoading(false) }
  }, [credentials])
  useEffect(() => {
    let active = true
    const request = credentials ? onboardingService.inspect(credentials)
      : staffSession ? onboardingService.own() : Promise.resolve(null)
    request.then((data) => {
      if (!active) return
      setWorkspace(data)
      setError('')
      setUncertain(false)
      setLoading(false)
    }).catch((failure) => {
      if (!active) return
      setWorkspace(null)
      setError(onboardingError(failure))
      setLoading(false)
    })
    return () => { active = false }
  }, [credentials, staffSession, session?.id])
  useEffect(() => {
    const acceptLink = () => { const next = takeLink(); if (next) setCredentials(next) }
    window.addEventListener('hashchange', acceptLink)
    return () => { linkCredentials = undefined; window.removeEventListener('hashchange', acceptLink) }
  }, [])

  const mutate = async (operation) => {
    if (guard.current || uncertain) return false
    guard.current = true
    setBusy(true)
    setError('')
    try { setWorkspace(await operation()); return true }
    catch (failure) {
      setError(onboardingError(failure))
      if (!failure.response) setUncertain(true)
      return false
    } finally { guard.current = false; setBusy(false) }
  }
  const revision = workspace?.revision
  const editable = revision && ['DRAFT', 'CHANGES_REQUESTED'].includes(revision.state)
    && (!credentials || (workspace.invitation?.status === 'PENDING' && new Date(workspace.invitation.expiresAtUtc) > new Date()))
  const canRead = credentials || staffSession
  return <div className="staff-onboarding-background"><ConfigProvider theme={onboardingTheme}><main className="staff-onboarding-page">
    <header><div><Typography.Title level={2}>Hồ sơ nhân viên</Typography.Title><Typography.Paragraph type="secondary">Hoàn thiện thông tin, kiểm tra và gửi hồ sơ để quản trị viên duyệt.</Typography.Paragraph></div><Link to={staffSession ? '/admin/staff/schedule' : '/dang-nhap'}>{staffSession ? 'Về lịch làm việc' : 'Đăng nhập sau khi được duyệt'}</Link></header>
    <Alert type="info" showIcon title="Thông tin dùng cho tiếp nhận nhân viên" description="Chỉ chủ hồ sơ và quản trị viên có quyền được truy cập. Ảnh CCCD là tùy chọn, giữ tối đa 30 ngày và thu hồi ngay khi duyệt, từ chối hoặc hủy. Hồ sơ pháp lý đã duyệt được khóa; hồ sơ cá nhân thông thường không thay thế bản pháp lý." />
    {!canRead && <Alert type="warning" showIcon title="Mở lại liên kết lời mời" description="Liên kết chỉ được giữ trong bộ nhớ. Sau khi tải lại trang, hãy mở lại email lời mời. Tài khoản khách hàng không cấp quyền nhân viên." />}
    {error && <Alert type="error" showIcon title="Không thể hoàn tất thao tác" description={error} />}
    {canRead && <Button disabled={busy || loading} onClick={reload}>Tải lại trạng thái</Button>}
    {uncertain && <Alert type="warning" showIcon title="Kết quả chưa xác định" description="Không gửi lại thao tác. Tải lại trạng thái trước khi tiếp tục." />}
    {canRead && (loading ? <Spin aria-label="Đang tải hồ sơ" /> : workspace && <>
      <Typography.Title level={3}>{stateLabels[revision?.state] || 'Hồ sơ hiện có'}</Typography.Title>
      {revision?.correctionReason && <Alert type="warning" showIcon title="Yêu cầu bổ sung" description={revision.correctionReason} />}
      <div className="onboarding-workspace onboarding-contract"><StaffContractSchedule contract={workspace.contract || workspace.invitation?.contract} shifts={workspace.shifts || workspace.invitation?.shifts || []} employeeId={!credentials && staffSession ? session.id : undefined} scope="self" /></div>
      {editable ? <OnboardingProfileForm key={revision.id} revision={revision} invitation={workspace.invitation} busy={busy} disabled={uncertain} requirePassword={Boolean(credentials)}
        onSave={(profile) => mutate(() => onboardingService.save(credentials, { profile, expectedVersion: revision.version }))}
        onSubmit={(data) => mutate(() => onboardingService.submit(credentials, { ...data, expectedVersion: revision.version }))}
        documents={<OnboardingDocuments documents={revision.documents} editable disabled={busy || uncertain} onUpload={(purpose, file) => mutate(() => onboardingService.upload(credentials, revision.version, purpose, file))} onRead={(id, signal) => onboardingService.readDocument(id, credentials, signal)} />} />
        : <><OnboardingRevisionDetails revision={revision || workspace.approvedRevision} /><OnboardingDocuments documents={(revision || workspace.approvedRevision)?.documents} onRead={(id, signal) => onboardingService.readDocument(id, credentials, signal)} /></>}
      {revision?.state === 'SUBMITTED' && <Alert type="info" title="Hồ sơ đang chờ quản trị viên duyệt" description="Bạn chỉ có thể chỉnh sửa khi quản trị viên yêu cầu bổ sung." />}
      {workspace.approvedRevision && revision?.id !== workspace.approvedRevision.id && <><Typography.Title level={3}>Bản đã duyệt đang có hiệu lực</Typography.Title><OnboardingRevisionDetails revision={workspace.approvedRevision} /></>}
      {!credentials && !editable && revision?.state !== 'SUBMITTED' && <section aria-label="Yêu cầu chỉnh sửa"><Typography.Title level={3}>Yêu cầu chỉnh sửa hồ sơ</Typography.Title><Typography.Paragraph>Bản đã duyệt vẫn có hiệu lực trong lúc chờ chỉnh sửa.</Typography.Paragraph>
        {workspace.correctionRequest ? <Alert type="info" title={stateLabels[workspace.correctionRequest.state] || workspace.correctionRequest.state} description={workspace.correctionRequest.reason} /> : <Space.Compact className="onboarding-reason"><Input aria-label="Lý do chỉnh sửa" maxLength={1000} value={reason} onChange={(event) => setReason(event.target.value)} /><Button disabled={busy || uncertain || !reason.trim()} onClick={() => mutate(() => onboardingService.requestCorrection({ reason: reason.trim() }))}>Gửi yêu cầu</Button></Space.Compact>}
      </section>}
    </>)}
  </main></ConfigProvider></div>
}
