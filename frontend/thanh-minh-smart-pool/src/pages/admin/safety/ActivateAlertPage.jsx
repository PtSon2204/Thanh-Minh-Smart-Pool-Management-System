import { useState, useCallback, useEffect, useRef } from 'react'
import { Input, Modal, message } from 'antd'
import {
  AlertOutlined,
  EnvironmentOutlined,
  UserOutlined,
  ClockCircleOutlined,
  StopOutlined,
} from '@ant-design/icons'
import dayjs from 'dayjs'
import { useAuthStore } from '../../../features/auth/store/authStore'
import { useAlarmSound } from '../../../features/safety/hooks/useAlarmSound'
import { useSafetyAlertSignalR } from '../../../features/safety/hooks/useSafetyAlertSignalR'
import safetyAlertService from '../../../features/safety/services/safetyAlertService'
import { isLocalSafetyAlertEcho, rememberLocalSafetyAlert } from '../../../features/safety/utils/localAlertEcho'
import './ActivateAlertPage.css'

const ZONES = [
  { value: 'Bể lớn', label: 'Bể lớn' },
  { value: 'Bể trẻ em', label: 'Bể trẻ em' },
]

const EMERGENCY_LEVEL = {
  value: 'EMERGENCY',
  label: 'Khẩn cấp',
  desc: 'Nguy hiểm tính mạng — cần ứng cứu ngay',
  color: '#dc2626',
  bg: '#fee2e2',
}

export default function ActivateAlertPage() {
  const { session } = useAuthStore()
  const { startAlarm, stopAlarm } = useAlarmSound()

  const [zone, setZone] = useState('Bể lớn')
  const [description, setDescription] = useState('')
  const [loading, setLoading] = useState(false)
  const [activeAlert, setActiveAlert] = useState(null)
  const [confirmOpen, setConfirmOpen] = useState(false)
  const stopTimerRef = useRef(null)

  // Nhận cảnh báo từ SignalR (từ thiết bị khác gửi tới)
  const handleIncomingAlert = useCallback(
    (payload) => {
      if (isLocalSafetyAlertEcho(payload)) {
        return
      }
      setActiveAlert(payload)
      startAlarm(payload.level || EMERGENCY_LEVEL.value)

      // Tự động tắt sau 30 giây
      if (stopTimerRef.current) clearTimeout(stopTimerRef.current)
      stopTimerRef.current = setTimeout(() => {
        stopAlarm()
        setActiveAlert(null)
      }, 30000)
    },
    [startAlarm, stopAlarm],
  )

  // Kết nối SignalR để nhận cảnh báo từ bất kỳ thiết bị nào
  useSafetyAlertSignalR({ onAlert: handleIncomingAlert, enabled: true })

  // Cleanup khi unmount
  useEffect(() => {
    return () => {
      if (stopTimerRef.current) clearTimeout(stopTimerRef.current)
      stopAlarm()
    }
  }, [stopAlarm])

  // Tắt cảnh báo thủ công (không tự chuyển trang)
  const handleStopAlarm = useCallback(() => {
    stopAlarm()
    setActiveAlert(null)
    if (stopTimerRef.current) {
      clearTimeout(stopTimerRef.current)
      stopTimerRef.current = null
    }
  }, [stopAlarm])

  // Mở modal xác nhận trước khi kích hoạt
  const handleActivateClick = useCallback(() => {
    setConfirmOpen(true)
  }, [])

  // Phát còi + overlay ngay lập tức và lưu sự cố vào lịch sử
  const handleConfirmActivate = useCallback(async () => {
    setConfirmOpen(false)
    setLoading(true)

    const localId = crypto.randomUUID()
    rememberLocalSafetyAlert(localId)

    const localPayload = {
      alertId: localId,
      clientRequestId: localId,
      zone,
      level: EMERGENCY_LEVEL.value,
      description: description.trim() || null,
      triggeredByName: session?.username || session?.fullName || 'Cứu hộ viên',
      triggeredAt: new Date().toISOString(),
    }

    // Phát âm thanh ngay trong user gesture trước khi await để tránh trình duyệt chặn autoplay
    setActiveAlert(localPayload)
    startAlarm(EMERGENCY_LEVEL.value)

    if (stopTimerRef.current) clearTimeout(stopTimerRef.current)
    stopTimerRef.current = setTimeout(() => {
      stopAlarm()
      setActiveAlert(null)
    }, 30000)

    try {
      await safetyAlertService.activate({
        clientRequestId: localId,
        zone,
        level: EMERGENCY_LEVEL.value,
        description: description.trim() || null,
      })
      message.success('Đã kích hoạt cảnh báo khẩn cấp và lưu vào lịch sử sự cố!')
    } catch (err) {
      console.warn('[SafetyAlert] activate backend error:', err)
      message.warning(
        err.response?.data?.message ||
          'Cảnh báo khẩn cấp đang phát tại máy này (chưa thể lưu lên máy chủ).',
      )
    } finally {
      setLoading(false)
    }
  }, [zone, description, session, startAlarm, stopAlarm])

  return (
    <div className="alert-page">
      {/* ── Active alert overlay ── */}
      {activeAlert && (
        <div className={`alert-active-overlay level-${(activeAlert.level || EMERGENCY_LEVEL.value).toLowerCase()}`}>
          <div className="alert-active-inner">
            <div className="alert-active-icon">
              <AlertOutlined />
            </div>
            <div className="alert-active-level">
              {activeAlert.level === 'EMERGENCY' ? 'KHẨN CẤP' : 'CẢNH BÁO'}
            </div>
            <div className="alert-active-zone">{activeAlert.zone}</div>
            {activeAlert.description && (
              <div className="alert-active-desc">{activeAlert.description}</div>
            )}
            <div className="alert-active-meta">
              <span>
                <UserOutlined /> {activeAlert.triggeredByName}
              </span>
              <span>
                <ClockCircleOutlined />{' '}
                {dayjs(activeAlert.triggeredAt).format('HH:mm:ss')}
              </span>
            </div>
            <button className="alert-stop-btn" onClick={handleStopAlarm}>
              <StopOutlined /> Tắt cảnh báo
            </button>
          </div>
        </div>
      )}

      {/* ── Page header ── */}
      <div className="alert-header">
        <div className="alert-header-icon">
          <AlertOutlined />
        </div>
        <div>
          <div className="alert-header-title">Kích hoạt cảnh báo cứu hộ</div>
          <div className="alert-header-sub">
            Nhân viên:{' '}
            <strong>{session?.username || session?.fullName || 'Cứu hộ viên'}</strong>
          </div>
        </div>
      </div>

      {/* ── Bố cục chính: Form bên trái & Nút tròn kích hoạt bên phải ── */}
      <div className="alert-main-layout">
        {/* CỘT BÊN TRÁI: FORM CẤU HÌNH */}
        <div className="alert-form-panel">
          {/* Khu vực sự cố */}
          <div className="alert-field">
            <label className="alert-field-label">
              <EnvironmentOutlined /> Khu vực sự cố
            </label>
            <div className="alert-zone-group">
              {ZONES.map((z) => (
                <button
                  key={z.value}
                  type="button"
                  className={`alert-zone-btn ${zone === z.value ? 'active' : ''}`}
                  onClick={() => setZone(z.value)}
                >
                  <EnvironmentOutlined />
                  <span>{z.label}</span>
                </button>
              ))}
            </div>
          </div>

          {/* Mức độ cảnh báo (chỉ Khẩn cấp) */}
          <div className="alert-field">
            <label className="alert-field-label">
              <AlertOutlined /> Mức độ cảnh báo
            </label>
            <div className="alert-level-group">
              <div
                className="alert-level-btn active"
                style={{
                  background: EMERGENCY_LEVEL.bg,
                  borderColor: EMERGENCY_LEVEL.color,
                  color: EMERGENCY_LEVEL.color,
                  cursor: 'default',
                }}
              >
                <span className="level-btn-label">{EMERGENCY_LEVEL.label}</span>
                <span className="level-btn-desc">{EMERGENCY_LEVEL.desc}</span>
              </div>
            </div>
          </div>

          {/* Mô tả (tuỳ chọn) */}
          <div className="alert-field">
            <label className="alert-field-label">Mô tả (tuỳ chọn)</label>
            <Input.TextArea
              placeholder="Ví dụ: Trẻ em đuối nước, chuột rút..."
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              rows={3}
              maxLength={200}
              showCount
              className="alert-textarea"
            />
          </div>
        </div>

        {/* CỘT BÊN PHẢI: NÚT KÍCH HOẠT HÌNH TRÒN */}
        <div className="alert-button-side">
          <div className="alert-circle-wrap">
            <button
              type="button"
              className={`alert-circle-btn level-emergency ${loading ? 'loading' : ''}`}
              onClick={handleActivateClick}
              disabled={loading}
              title="Nhấn để kích hoạt cảnh báo cứu hộ ngay"
            >
              <span className="circle-btn-ripple" />
              <span className="circle-btn-icon">
                <AlertOutlined />
              </span>
              <span className="circle-btn-title">
                {loading ? 'ĐANG PHÁT...' : 'KÍCH HOẠT NGAY'}
              </span>
              <span className="circle-btn-sub">
                {EMERGENCY_LEVEL.label} • {zone}
              </span>
            </button>
          </div>
          <p className="alert-circle-hint">
            Nhấn để phát cảnh báo âm thanh tới tất cả thiết bị
          </p>
        </div>
      </div>

      {/* ── Confirmation modal ── */}
      <Modal
        className="safety-modal"
        open={confirmOpen}
        onCancel={() => setConfirmOpen(false)}
        onOk={handleConfirmActivate}
        okText="XÁC NHẬN KÍCH HOẠT"
        cancelText="Huỷ"
        okButtonProps={{
          danger: true,
          size: 'large',
          style: { fontWeight: 700 },
        }}
        title={
          <span style={{ color: EMERGENCY_LEVEL.color, fontWeight: 800, fontSize: 18 }}>
            Xác nhận kích hoạt cảnh báo
          </span>
        }
        centered
        width={400}
      >
        <div className="confirm-modal-body">
          <div className="confirm-row">
            <span className="confirm-label">Khu vực:</span>
            <strong>{zone}</strong>
          </div>
          <div className="confirm-row">
            <span className="confirm-label">Mức độ:</span>
            <strong style={{ color: EMERGENCY_LEVEL.color }}>{EMERGENCY_LEVEL.label}</strong>
          </div>
          {description && (
            <div className="confirm-row">
              <span className="confirm-label">Mô tả:</span>
              <span>{description}</span>
            </div>
          )}
          <div className="confirm-row">
            <span className="confirm-label">Kích hoạt bởi:</span>
            <strong>{session?.username || 'Nhân viên'}</strong>
          </div>
          <p className="confirm-warning">
            Âm thanh cảnh báo sẽ phát ngay lập tức trên tất cả thiết bị đang kết nối và ghi nhận vào lịch sử sự cố.
          </p>
        </div>
      </Modal>
    </div>
  )
}
