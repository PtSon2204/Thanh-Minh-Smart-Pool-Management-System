import { useEffect, useRef, useState } from 'react'
import { Alert, Button, ConfigProvider, Input, Space, Spin, Typography } from 'antd'
import { Link } from 'react-router-dom'
import { useAuthStore } from '../../features/auth/store/authStore'
import OnboardingProfileForm from '../../features/staff/components/OnboardingProfileForm'
import OnboardingDocuments from '../../features/staff/components/OnboardingDocuments'
import OnboardingRevisionDetails from '../../features/staff/components/OnboardingRevisionDetails'
import StaffContractSchedule from '../../features/staff/components/StaffContractSchedule'
import { onboardingService } from '../../features/staff/services/onboardingService'
import { isUnavailableOnboardingLink, onboardingError, onboardingGuidance, stateLabels, unavailableOnboardingGuidance } from '../../features/staff/onboarding/onboardingUi'
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
  const [view, setView] = useState(null)
  const [reloadVersion, setReloadVersion] = useState(0)
  const [busy, setBusy] = useState(false)
  const [reason, setReason] = useState('')
  const guard = useRef(false)
  const [uncertain, setUncertain] = useState(false)

  const source = credentials || (staffSession ? session.id : null)
  const currentView = view?.source === source ? view : null
  const workspace = currentView?.workspace
  const error = currentView?.error
  const loading = !currentView
  const reload = () => {
    setView(null)
    setReloadVersion((version) => version + 1)
  }
  useEffect(() => {
    let active = true
    const request = credentials ? onboardingService.inspect(credentials)
      : staffSession ? onboardingService.own() : Promise.resolve(null)
    request.then((data) => {
      if (!active) return
      setView({ source, workspace: data })
      setUncertain(false)
    }).catch((failure) => {
      if (!active) return
      setView({ source, workspace: null, error: failure })
    })
    return () => { active = false }
  }, [credentials, staffSession, source, reloadVersion])
  useEffect(() => {
    const acceptLink = () => { const next = takeLink(); if (next) setCredentials(next) }
    window.addEventListener('hashchange', acceptLink)
    return () => { linkCredentials = undefined; window.removeEventListener('hashchange', acceptLink) }
  }, [])

  const mutate = async (operation) => {
    if (guard.current || uncertain) return false
    guard.current = true
    setBusy(true)
    setView((current) => ({ ...current, error: null }))
    try {
      const data = await operation()
      setView((current) => current?.source === source ? { source, workspace: data } : current)
      return true
    }
    catch (failure) {
      setView((current) => current?.source === source ? {
        ...current, error: failure,
        workspace: credentials && isUnavailableOnboardingLink(failure) ? null : current.workspace,
      } : current)
      if (!failure.response) setUncertain(true)
      return false
    } finally { guard.current = false; setBusy(false) }
  }
  const revision = workspace?.revision
  const canRead = credentials || staffSession
  const unavailableLink = Boolean(credentials && isUnavailableOnboardingLink(error))
  const guidance = workspace ? onboardingGuidance(workspace, Boolean(credentials)) : null
  const editable = guidance?.editable
  return <div className="staff-onboarding-background"><ConfigProvider theme={onboardingTheme}><main className="staff-onboarding-page">
    <header><Typography.Title level={2}>Hồ sơ nhân viên</Typography.Title><Link to={staffSession ? '/admin/staff/schedule' : '/dang-nhap'}>{staffSession ? 'Về lịch làm việc' : 'Đăng nhập'}</Link></header>
    <Alert type="info" showIcon title="Thông tin dùng cho tiếp nhận nhân viên" description="Chỉ chủ hồ sơ và quản trị viên có quyền được truy cập. Ảnh CCCD là tùy chọn, giữ tối đa 30 ngày và thu hồi ngay khi duyệt, từ chối hoặc hủy. Hồ sơ pháp lý đã duyệt được khóa; hồ sơ cá nhân thông thường không thay thế bản pháp lý." />
    {(!canRead || unavailableLink) && <Alert showIcon {...unavailableOnboardingGuidance} />}
    {error && !unavailableLink && <Alert type="error" showIcon title={workspace ? 'Không thể hoàn tất thao tác' : 'Không thể tải trạng thái hồ sơ'} description={!workspace && !error.response ? 'Không thể kết nối đến máy chủ. Kiểm tra kết nối và thử lại.' : onboardingError(error)} />}
    {canRead && <Button disabled={busy || loading} onClick={reload}>{error && !unavailableLink ? 'Thử lại' : 'Tải lại trạng thái'}</Button>}
    {uncertain && <Alert type="warning" showIcon title="Kết quả chưa xác định" description="Không gửi lại thao tác. Tải lại trạng thái trước khi tiếp tục." />}
    {canRead && (loading ? <Spin aria-label="Đang tải hồ sơ" /> : workspace && !unavailableLink && <>
      <Alert type={guidance.type} showIcon title={guidance.title} description={guidance.description} action={guidance.canLogin && <Link to="/dang-nhap">Đăng nhập</Link>} />
      {guidance.showProfile && <>
      {revision?.correctionReason && <Alert type="warning" showIcon title="Yêu cầu bổ sung" description={revision.correctionReason} />}
      <div className="onboarding-workspace onboarding-contract"><StaffContractSchedule contract={workspace.contract || workspace.invitation?.contract} shifts={workspace.shifts || workspace.invitation?.shifts || []} employeeId={!credentials && staffSession ? session.id : undefined} scope="self" /></div>
      {editable ? <OnboardingProfileForm key={revision.id} revision={revision} invitation={workspace.invitation} busy={busy} disabled={uncertain} requirePassword={Boolean(credentials)}
        onSave={(profile) => mutate(() => onboardingService.save(credentials, { profile, expectedVersion: revision.version }))}
        onSubmit={(data) => mutate(() => onboardingService.submit(credentials, { ...data, expectedVersion: revision.version }))}
        documents={<OnboardingDocuments documents={revision.documents} editable disabled={busy || uncertain} onUpload={(purpose, file) => mutate(() => onboardingService.upload(credentials, revision.version, purpose, file))} onRead={(id, signal) => onboardingService.readDocument(id, credentials, signal)} />} />
        : <><OnboardingRevisionDetails revision={revision || workspace.approvedRevision} /><OnboardingDocuments documents={(revision || workspace.approvedRevision)?.documents} onRead={(id, signal) => onboardingService.readDocument(id, credentials, signal)} /></>}
      {workspace.approvedRevision && revision?.id !== workspace.approvedRevision.id && <><Typography.Title level={3}>Bản đã duyệt đang có hiệu lực</Typography.Title><OnboardingRevisionDetails revision={workspace.approvedRevision} /></>}
      {!credentials && !editable && revision?.state !== 'SUBMITTED' && <section aria-label="Yêu cầu chỉnh sửa"><Typography.Title level={3}>Yêu cầu chỉnh sửa hồ sơ</Typography.Title>
        {workspace.correctionRequest ? <Alert type="info" title={stateLabels[workspace.correctionRequest.state] || workspace.correctionRequest.state} description={workspace.correctionRequest.reason} /> : <Space.Compact className="onboarding-reason"><Input aria-label="Lý do chỉnh sửa" maxLength={1000} value={reason} onChange={(event) => setReason(event.target.value)} /><Button disabled={busy || uncertain || !reason.trim()} onClick={() => mutate(() => onboardingService.requestCorrection({ reason: reason.trim() }))}>Gửi yêu cầu</Button></Space.Compact>}
      </section>}
      </>}
    </>)}
  </main></ConfigProvider></div>
}
