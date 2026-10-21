import { useCallback, useEffect, useMemo, useRef, useState } from 'react'
import { Alert, Button, Descriptions, Empty, Spin, Tag } from 'antd'
import { Link } from 'react-router-dom'
import { useAuthStore } from '../../features/auth/store/authStore'
import QrScanner from '../../features/qr/QrScanner'
import { QR_PURPOSE } from '../../features/qr/qrPurpose'
import { findPreviewAssignment, isQrPreviewActive, normalizeAttendanceAction, reconcileAttendanceScan, shouldShowExpiredQrNotice } from '../../features/staff/hooks/attendanceQrScanState.js'
import { useStaffSchedulePage } from '../../features/staff/hooks/useStaffs'
import { attendanceQrService } from '../../features/staff/services/attendanceQrService.js'
import staffService from '../../features/staff/services/staffService'
import { formatDate, formatTime, isOvernight, statusColor, statusLabel, vietnamToday } from '../../features/staff/schedule/scheduleUtils'
import { ServiceListHeader } from '../../features/services/components/ServiceListChrome'
import '../../features/services/styles/serviceManagement.css'
import '../../features/shared/styles/adminActions.css'
import '../../features/staff/styles/staffSchedule.css'

const VERIFIED_QR_PURPOSE = 'StaffAttendance'

function formatServerTime(value) {
  if (!value || !Number.isFinite(Date.parse(value))) return 'Không xác định'
  return new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'medium', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(value))
}

function assignmentSummary(assignment) {
  const overnight = isOvernight(assignment) ? ' · Qua ngày' : ''
  return `${assignment.shiftName || 'Ca làm việc'} · ${formatTime(assignment.startTime)} - ${formatTime(assignment.endTime)}${overnight}`
}

export default function StaffAttendanceScanPage() {
  const session = useAuthStore((state) => state.session)
  const role = session?.role?.toUpperCase()
  const canScan = role === 'SALES' || role === 'LIFEGUARD'
  const [preview, setPreview] = useState(null)
  const [phase, setPhase] = useState('idle')
  const [notice, setNotice] = useState(null)
  const [locked, setLocked] = useState(false)
  const [now, setNow] = useState(() => Date.now())
  const submitClaimRef = useRef(false)
  const ownScheduleDate = preview?.verified.workDate || vietnamToday()
  const scheduleRange = useMemo(() => ({ fromDate: ownScheduleDate, toDate: ownScheduleDate }), [ownScheduleDate])
  const ownSchedule = useStaffSchedulePage({
    scope: 'self',
    sessionUserId: session?.id,
    params: scheduleRange,
    enabled: canScan,
  })
  const assignments = ownSchedule.data?.items || []
  const assignment = preview ? findPreviewAssignment(assignments, preview.verified) : null
  const currentState = preview ? reconcileAttendanceScan(assignments, preview.verified) : null
  const tokenIsValid = preview && isQrPreviewActive(preview.verified, now)
  const canConfirm = Boolean(phase === 'ready' && !locked && tokenIsValid && assignment && currentState === 'unchanged' && ownSchedule.isSuccess && !ownSchedule.isFetching)

  useEffect(() => {
    if (!preview) return undefined
    const timer = window.setInterval(() => setNow(Date.now()), 1_000)
    return () => window.clearInterval(timer)
  }, [preview])

  const readExactOwnSchedule = useCallback(async (verified) => {
    const result = await staffService.getCompleteSchedule('self', { fromDate: verified.workDate, toDate: verified.workDate })
    return result.items || []
  }, [])

  const handleDecoded = useCallback(async (qrPayload) => {
    if (locked || phase === 'previewing' || phase === 'submitting' || !canScan) return
    setNotice(null)
    setPreview(null)
    setPhase('previewing')

    try {
      const response = await attendanceQrService.preview(qrPayload)
      const action = normalizeAttendanceAction(response.action)
      if (response.purpose !== VERIFIED_QR_PURPOSE || !response.shiftId || !response.workDate || !action) {
        setPhase('readOnly')
        setNotice({ type: 'error', title: 'Bối cảnh QR không hợp lệ', description: 'Máy chủ không xác nhận đúng mục đích chấm công. Không gửi yêu cầu.' })
        return
      }
      const verified = { ...response, action }
      setPreview({ qrPayload, verified })
      setPhase('ready')
    } catch {
      setPhase('readOnly')
      setNotice({ type: 'error', title: 'Không thể xác minh mã QR', description: 'Máy chủ không xác minh được mã. Không gửi yêu cầu chấm công.' })
    }
  }, [canScan, locked, phase])

  const confirmScan = useCallback(async () => {
    if (!preview || phase !== 'ready' || locked || submitClaimRef.current) return
    if (!isQrPreviewActive(preview.verified, Date.now())) {
      setPhase('readOnly')
      setNotice({ type: 'warning', title: 'Mã QR đã hết hạn', description: 'Không gửi yêu cầu từ mã hoặc phiên đã hết hạn. Hãy quét mã mới.' })
      return
    }
    if (!canConfirm) return
    submitClaimRef.current = true
    setLocked(true)
    setNotice(null)
    setPhase('submitting')

    try {
      await attendanceQrService.scan(preview.qrPayload)
      setPhase('reconciling')
      const freshAssignments = await readExactOwnSchedule(preview.verified)
      await ownSchedule.refetch()
      const observed = reconcileAttendanceScan(freshAssignments, preview.verified)
      if (observed === 'recorded') {
        setPhase('complete')
        setNotice({ type: 'success', title: 'Lịch cá nhân đã phản ánh thao tác chấm công', description: 'Trạng thái hiển thị là dữ liệu lịch cá nhân vừa tải lại.' })
        return
      }
      setPhase('readOnly')
      setNotice({ type: 'warning', title: 'Không thể xác nhận trạng thái sau yêu cầu', description: 'Lịch cá nhân không phản ánh đúng trạng thái sau thao tác. Màn hình bị khóa để an toàn.' })
    } catch {
      setPhase('reconciling')
      let observed
      try {
        const freshAssignments = await readExactOwnSchedule(preview.verified)
        const refreshed = await ownSchedule.refetch()
        observed = refreshed.error ? 'unavailable' : reconcileAttendanceScan(freshAssignments, preview.verified)
      } catch {
        observed = 'unavailable'
      }
      if (observed === 'recorded') {
        setPhase('complete')
        setNotice({ type: 'success', title: 'Lịch cá nhân đã có thao tác này', description: 'Không thể gán kết quả đó cho yêu cầu vừa gửi. Không gửi lại tự động.' })
        return
      }
      if (observed === 'unchanged' && isQrPreviewActive(preview.verified, Date.now())) {
        submitClaimRef.current = false
        setLocked(false)
        setPhase('ready')
        setNotice({ type: 'warning', title: 'Kết quả gửi yêu cầu chưa xác định', description: 'Lịch cá nhân vẫn ở trạng thái trước thao tác. Chỉ bấm xác nhận lại nếu muốn gửi một yêu cầu mới.' })
        return
      }
      setPhase('readOnly')
      setNotice({ type: 'warning', title: 'Không thể đối soát an toàn', description: 'Không thể xác nhận trạng thái lịch cá nhân. Màn hình giữ ở chế độ chỉ đọc.' })
    }
  }, [canConfirm, locked, ownSchedule, phase, preview, readExactOwnSchedule])

  const resetScanner = () => {
    submitClaimRef.current = false
    setLocked(false)
    setPreview(null)
    setNotice(null)
    setPhase('idle')
  }

  if (!canScan) return <div className="service-list-page staff-schedule-page"><Alert type="warning" showIcon title="Không có quyền truy cập" description="Chỉ nhân viên bán hàng và nhân viên cứu hộ có thể quét QR chấm công cá nhân." /></div>

  return <div className="service-list-page staff-schedule-page staff-attendance-scan-page">
    <ServiceListHeader title="Quét QR chấm công cá nhân" action={<Link to="/admin/staff/schedule"><Button>Lịch làm việc của tôi</Button></Link>} />
    <section className="service-list-table staff-schedule-table" aria-label="Lịch làm việc của tôi">
      <Alert type="info" showIcon title="Lịch làm việc của tôi" description="Lịch luôn hiển thị khi máy ảnh không khả dụng. QR không tự xác nhận có mặt thực tế." />
      {ownSchedule.isLoading && <div className="admin-management-state"><Spin size="large" /></div>}
      {ownSchedule.isError && <Empty description="Không thể tải lịch làm việc cá nhân."><Button onClick={() => ownSchedule.refetch()}>Thử lại</Button></Empty>}
      {!ownSchedule.isLoading && !ownSchedule.isError && assignments.length === 0 && <Empty description={`Chưa có phân công cá nhân cho ${formatDate(ownScheduleDate)}.`} />}
      {!ownSchedule.isLoading && !ownSchedule.isError && assignments.map((item) => <article className="staff-week-shift-card" key={item.id}>
        <header className="staff-week-shift-heading"><strong>{item.shiftName || 'Ca làm việc'}</strong><Tag color={statusColor(item.status)}>{statusLabel(item.status)}</Tag></header>
        <span>{formatDate(item.workDate)} · {assignmentSummary(item)}</span>
      </article>)}
    </section>

    <section className="service-list-table staff-schedule-table" aria-label="Quét và xác nhận chấm công">
      <h2>Quét mã QR</h2>
      <p>Máy chủ xác minh ca, ngày và thao tác. Ứng dụng chỉ gửi mã QR; không gửi mã nhân viên, phân công, thời gian hoặc thao tác.</p>
      {phase === 'idle' && <Button type="primary" onClick={() => { setNotice(null); setPhase('scanning') }}>Bắt đầu quét</Button>}
      {phase === 'scanning' && <>
        <Button onClick={resetScanner}>Dừng quét</Button>
        <QrScanner expectedPurpose={QR_PURPOSE.staff} onDecoded={handleDecoded} />
      </>}
      {phase === 'previewing' && <div className="admin-management-state"><Spin tip="Đang xác minh mã QR với máy chủ" /></div>}
      {preview && <Descriptions className="staff-schedule-alert" size="small" bordered column={{ xs: 1, sm: 2 }} title="Bối cảnh máy chủ đã xác minh">
        <Descriptions.Item label="Mục đích">{preview.verified.purpose}</Descriptions.Item>
        <Descriptions.Item label="Ca">{assignment ? assignmentSummary(assignment) : `Mã ca ${preview.verified.shiftId}`}</Descriptions.Item>
        <Descriptions.Item label="Ngày làm">{formatDate(preview.verified.workDate)}</Descriptions.Item>
        <Descriptions.Item label="Thao tác">{preview.verified.action === 'CheckIn' ? 'Vào ca' : 'Ra ca'}</Descriptions.Item>
        <Descriptions.Item label="Token hết hạn">{formatServerTime(preview.verified.tokenExpiresAtUtc)}</Descriptions.Item>
        <Descriptions.Item label="Phiên hết hạn">{formatServerTime(preview.verified.sessionExpiresAtUtc)}</Descriptions.Item>
      </Descriptions>}
      {phase === 'ready' && ownSchedule.isFetching && <Spin tip="Đang khớp ca với lịch cá nhân" />}
      {preview && !ownSchedule.isLoading && !ownSchedule.isError && !assignment && <Alert className="staff-schedule-alert" type="warning" showIcon title="Không có phân công cá nhân khớp mã QR" description="Không gửi yêu cầu chấm công. Lịch cá nhân không có ca đúng với ngày và mã ca do máy chủ xác minh." />}
      {preview && assignment && currentState !== 'unchanged' && currentState !== 'recorded' && <Alert className="staff-schedule-alert" type="warning" showIcon title="Trạng thái ca không cho phép chấm công" description="Lịch cá nhân khác với trạng thái cần thiết. Không gửi yêu cầu chấm công." />}
      {shouldShowExpiredQrNotice(phase, preview, tokenIsValid) && <Alert className="staff-schedule-alert" type="warning" showIcon title="Mã QR đã hết hạn" description="Không gửi yêu cầu từ mã hoặc phiên đã hết hạn. Hãy quét mã mới." />}
      {notice && <Alert className="staff-schedule-alert" type={notice.type} showIcon title={notice.title} description={notice.description} />}
      {canConfirm && <div className="admin-action-group"><Button className="admin-action-button admin-action-button--primary" type="primary" onClick={confirmScan} loading={phase === 'submitting'}>Xác nhận chấm công</Button></div>}
      {(phase === 'complete' || phase === 'readOnly' || (preview && !tokenIsValid)) && <div className="admin-action-group staff-schedule-alert"><Button onClick={resetScanner}>Quét mã khác</Button></div>}
    </section>
  </div>
}
