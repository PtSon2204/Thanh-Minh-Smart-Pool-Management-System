import { useState, useCallback, useEffect, useRef } from 'react'
import { Input, Modal, message } from 'antd'
import {
  AlertOutlined,
  EnvironmentOutlined,
  UserOutlined,
  ClockCircleOutlined,
  CheckCircleOutlined,
  StopOutlined,
} from '@ant-design/icons'
import dayjs from 'dayjs'
import { useAuthStore } from '../../../features/auth/store/authStore'
import { useAlarmSound } from '../../../features/safety/hooks/useAlarmSound'
import { useSafetyAlertSignalR } from '../../../features/safety/hooks/useSafetyAlertSignalR'
import safetyAlertService from '../../../features/safety/services/safetyAlertService'
import './ActivateAlertPage.css'

const ZONES = [
  { value: 'Bể lớn', label: 'Bể lớn' },
  { value: 'Bể trẻ em', label: 'Bể trẻ em' },
]

const LEVELS = [
  {
    value: 'EMERGENCY',
    label: 'Khẩn cấp',
    desc: 'Nguy hiểm tính mạng — cần ứng cứu ngay',
    color: '#dc2626',
    bg: '#fee2e2',
  },
  {
    value: 'WARNING',
    label: 'Cảnh báo',
    desc: 'Tình huống nguy hiểm, cần chú ý',
    color: '#d97706',
    bg: '#fef9c3',
  },
]

export default function ActivateAlertPage() {
  const { session } = useAuthStore()
  const { startAlarm, stopAlarm } = useAlarmSound()

  const [zone, setZone] = useState('Bể lớn')
  const [level, setLevel] = useState('EMERGENCY')
  const [description, setDescription] = useState('')
  const [loading, setLoading] = useState(false)
  const [activeAlert, setActiveAlert] = useState(null)
  const [alertHistory, setAlertHistory] = useState([])
  const [confirmOpen, setConfirmOpen] = useState(false)
  const stopTimerRef = useRef(null)

  const selectedLevel = LEVELS.find((l) => l.value === level) || LEVELS[0]

  // ID của alert local vừa kích hoạt, tránh bị echo lặp lại từ SignalR
  const lastLocalAlertIdRef = useRef(null)

  // Nhận cảnh báo từ SignalR (từ thiết bị khác gửi tới)
  const handleIncomingAlert = useCallback(
    (payload) => {
      if (payload?.alertId && payload.alertId === lastLocalAlertIdRef.current) {
        return
      }
      setActiveAlert(payload)
      setAlertHistory((prev) => [payload, ...prev].slice(0, 20))
      startAlarm(payload.level)

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

  // Tắt cảnh báo thủ công
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

  // Gọi API kích hoạt sau khi xác nhận trong Modal
  const handleConfirmActivate = useCallback(async () => {
    setConfirmOpen(false)
    setLoading(true)

    const localId = `alert-${Date.now()}`
    lastLocalAlertIdRef.current = localId

    const localPayload = {
      alertId: localId,
      zone,
      level,
      description: description.trim() || null,
      triggeredByName: session?.username || session?.fullName || 'Cứu hộ viên',
      triggeredAt: new Date().toISOString(),
    }

    // 1. PHÁT ÂM THANH VÀ BẬT OVERLAY NGAY TẠI THIẾT BỊ NÀY
    setActiveAlert(localPayload)
    setAlertHistory((prev) => [localPayload, ...prev].slice(0, 20))
    startAlarm(level)

    // Tự động tắt sau 30 giây
    if (stopTimerRef.current) clearTimeout(stopTimerRef.current)
    stopTimerRef.current = setTimeout(() => {
      stopAlarm()
      setActiveAlert(null)
    }, 30000)

    // 2. GỬI LÊN MÁY CHỦ ĐỂ PHÁT TỚI CÁC THIẾT BỊ KHÁC
    try {
      await safetyAlertService.activate({
        zone,
        level,
        description: description.trim() || null,
      })
      message.success('Đã kích hoạt cảnh báo tới toàn bộ hệ thống!')
    } catch (err) {
      console.warn('[SafetyAlert] activate backend error:', err)
      message.warning('Cảnh báo khẩn cấp đang phát tại máy này (chưa thể kết nối tới máy chủ).')
    } finally {
      setLoading(false)
    }
  }, [zone, level, description, session, startAlarm, stopAlarm])

  return (
    <div className="alert-page">
      {/* ── Active alert overlay ── */}
      {activeAlert && (
        <div className={`alert-active-overlay level-${activeAlert.level.toLowerCase()}`}>
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

          {/* Mức độ cảnh báo */}
          <div className="alert-field">
            <label className="alert-field-label">
              <AlertOutlined /> Mức độ cảnh báo
            </label>
            <div className="alert-level-group">
              {LEVELS.map((lv) => (
                <button
                  key={lv.value}
                  type="button"
                  className={`alert-level-btn ${level === lv.value ? 'active' : ''}`}
                  style={
                    level === lv.value
                      ? { background: lv.bg, borderColor: lv.color, color: lv.color }
                      : {}
                  }
                  onClick={() => setLevel(lv.value)}
                >
                  <span className="level-btn-label">{lv.label}</span>
                  <span className="level-btn-desc">{lv.desc}</span>
                </button>
              ))}
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
              className={`alert-circle-btn level-${selectedLevel.value.toLowerCase()} ${loading ? 'loading' : ''}`}
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
                {selectedLevel.label} • {zone}
              </span>
            </button>
          </div>
          <p className="alert-circle-hint">
            Nhấn để phát cảnh báo âm thanh tới tất cả thiết bị
          </p>
        </div>
      </div>

      {/* ── Lịch sử cảnh báo trong phiên ── */}
      {alertHistory.length > 0 && (
        <div className="alert-history">
          <div className="alert-history-title">
            <CheckCircleOutlined /> Lịch sử cảnh báo trong phiên này
          </div>
          {alertHistory.map((a, i) => (
            <div key={a.alertId || i} className={`alert-history-item level-${a.level?.toLowerCase()}`}>
              <div className="history-item-left">
                <span className="history-level-badge">
                  {a.level === 'EMERGENCY' ? 'Khẩn cấp' : 'Cảnh báo'}
                </span>
                <span className="history-zone">{a.zone}</span>
                {a.description && (
                  <span className="history-desc">{a.description}</span>
                )}
              </div>
              <div className="history-item-right">
                <span className="history-who">
                  <UserOutlined /> {a.triggeredByName}
                </span>
                <span className="history-time">
                  <ClockCircleOutlined />{' '}
                  {dayjs(a.triggeredAt).format('HH:mm:ss')}
                </span>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* ── Confirmation modal ── */}
      <Modal
        open={confirmOpen}
        onCancel={() => setConfirmOpen(false)}
        onOk={handleConfirmActivate}
        okText="XÁC NHẬN KÍCH HOẠT"
        cancelText="Huỷ"
        okButtonProps={{
          danger: selectedLevel.value === 'EMERGENCY',
          size: 'large',
          style: { fontWeight: 700 },
        }}
        title={
          <span style={{ color: selectedLevel.color, fontWeight: 800, fontSize: 18 }}>
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
            <strong style={{ color: selectedLevel.color }}>{selectedLevel.label}</strong>
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
            Âm thanh cảnh báo sẽ phát ngay lập tức trên tất cả thiết bị đang kết nối.
          </p>
        </div>
      </Modal>
    </div>
  )
}
