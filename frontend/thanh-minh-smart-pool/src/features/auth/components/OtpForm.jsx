import { useEffect, useRef, useState } from 'react'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { ArrowLeft, ArrowRight, MailCheck, ShieldCheck } from 'lucide-react'
import { useResendPasswordReset, useResendRegistration, useVerifyPasswordReset, useVerifyRegistration } from '../hooks/useAuthMutations'
import { clearPendingRegistration, readPendingRegistration, savePendingRegistration } from "../state/registrationSession"
import { savePasswordResetGrant } from "../state/passwordResetGrant"
import { getOtpRequest } from "../state/otpRequest"
import { getAuthError, registerPendingResponseSchema, requestPasswordResetResponseSchema } from '../types/authSchemas'

const OTP_LENGTH = 6

function initialRegistration(state) {
  const stored = readPendingRegistration()
  const parsed = registerPendingResponseSchema.safeParse(state)
  const fromNavigation = parsed.success && Date.parse(parsed.data.registrationExpiresAtUtc) > Date.now()
    ? { ...parsed.data, email: state.email }
    : null
  if (fromNavigation && stored?.registrationId !== fromNavigation.registrationId) return fromNavigation
  return stored || fromNavigation
}

function initialRecovery(state) {
  const parsed = requestPasswordResetResponseSchema.safeParse(state)
  return parsed.success && typeof state?.email === 'string' &&
    Date.parse(parsed.data.recoveryExpiresAtUtc) > Date.now()
    ? { ...parsed.data, email: state.email }
    : null
}

function formatCountdown(seconds) {
  const minutes = Math.floor(seconds / 60)
  const rest = seconds % 60
  return String(minutes).padStart(2, '0') + ':' + String(rest).padStart(2, '0')
}

export default function OtpForm({ flow }) {
  const location = useLocation()
  const navigate = useNavigate()
  const inputs = useRef([])
  const isRegister = flow === 'register'
  const verifyRegistration = useVerifyRegistration()
  const resendRegistration = useResendRegistration()
  const verifyRecovery = useVerifyPasswordReset()
  const resendRecovery = useResendPasswordReset()
  const verify = isRegister ? verifyRegistration : verifyRecovery
  const resend = isRegister ? resendRegistration : resendRecovery
  const [digits, setDigits] = useState(Array(OTP_LENGTH).fill(''))
  const [session, setSession] = useState(() => location.state?.requestId ? null
    : isRegister ? initialRegistration(location.state) : initialRecovery(location.state))
  const [requesting, setRequesting] = useState(() => Boolean(
    location.state?.requestId && getOtpRequest(flow, location.state.requestId)))
  const [requestError, setRequestError] = useState(null)
  const [feedback, setFeedback] = useState(null)
  const [locked, setLocked] = useState(false)
  const [resendExhausted, setResendExhausted] = useState(false)
  const [now, setNow] = useState(Date.now)
  const pending = verify.isPending || resend.isPending
  const email = session?.email
  const expired = session && Date.parse(isRegister ? session.registrationExpiresAtUtc : session.recoveryExpiresAtUtc) <= now
  const otpExpired = session && Date.parse(session.otpExpiresAtUtc) <= now
  const resendWait = session
    ? Math.max(0, Math.ceil((Date.parse(session.resendAvailableAtUtc) - now) / 1000))
    : 0
  const backTo = isRegister ? '/dang-ky' : '/quen-mat-khau'
  const backLabel = isRegister ? 'đăng ký' : 'quên mật khẩu'

  useEffect(() => {
    const timer = window.setInterval(() => setNow(Date.now()), 1000)
    return () => window.clearInterval(timer)
  }, [])

  useEffect(() => {
    const requestId = location.state?.requestId
    if (!requestId) return
    const request = getOtpRequest(flow, requestId)
    if (!request) return
    let active = true
    request.result.then(({ session: readySession, error }) => {
      if (!active) return
      setRequesting(false)
      if (error) setRequestError(getAuthError(error).message)
      else {
        setSession(readySession)
        navigate(location.pathname, { replace: true, state: readySession })
      }
    })
    return () => { active = false }
  }, [flow, location.pathname, location.state?.requestId, navigate])

  useEffect(() => {
    if (expired && isRegister) clearPendingRegistration()
  }, [expired, isRegister])

  function fillFrom(index, value) {
    const numbers = value.replace(/\D/g, '').slice(0, OTP_LENGTH - index)
    if (!numbers) return
    setDigits((current) => {
      const next = [...current]
      for (let offset = 0; offset < numbers.length; offset += 1) next[index + offset] = numbers[offset]
      return next
    })
    setFeedback(null)
    inputs.current[Math.min(index + numbers.length, OTP_LENGTH - 1)]?.focus()
  }

  function handleKeyDown(event, index) {
    if (event.key === 'Backspace' && !digits[index] && index > 0) {
      event.preventDefault()
      setDigits((current) => current.map((digit, position) => position === index - 1 ? '' : digit))
      inputs.current[index - 1]?.focus()
      setFeedback(null)
    } else if (event.key === 'ArrowLeft' && index > 0) {
      event.preventDefault()
      inputs.current[index - 1]?.focus()
    } else if (event.key === 'ArrowRight' && index < OTP_LENGTH - 1) {
      event.preventDefault()
      inputs.current[index + 1]?.focus()
    }
  }

  function handleOtpError(error) {
    const code = error?.response?.data?.code
    const authError = getAuthError(error)
    if (code === 'REGISTRATION_UNAVAILABLE' || code === 'RECOVERY_UNAVAILABLE') {
      if (isRegister) clearPendingRegistration()
      setSession(null)
    }
    if (code === 'VERIFY_ATTEMPTS_EXCEEDED') setLocked(true)
    if (code === 'RESEND_LIMIT_REACHED') setResendExhausted(true)
    if (code === 'RESEND_COOLDOWN' || code === 'RESEND_IN_PROGRESS' || code === 'RECOVERY_IN_PROGRESS') {
      const headers = error?.response?.headers
      const retryAfter = Number(headers?.get?.('retry-after') ?? headers?.['retry-after'])
      if (session && Number.isFinite(retryAfter) && retryAfter > 0) {
        const updated = { ...session, resendAvailableAtUtc: new Date(Date.now() + retryAfter * 1000).toISOString() }
        setSession(updated)
        if (isRegister) savePendingRegistration(updated)
      }
    }
    setFeedback({ message: authError.message, error: true })
  }

  async function submit(event) {
    event.preventDefault()
    if (!session || expired) {
      setFeedback({ message: `Phiên ${isRegister ? 'đăng ký' : 'khôi phục'} đã hết hạn. Vui lòng bắt đầu lại.`, error: true })
      return
    }
    if (otpExpired) {
      setFeedback({ message: 'Mã OTP đã hết hạn. Vui lòng gửi lại mã.', error: true })
      return
    }
    if (locked || pending) return
    if (digits.some((digit) => !digit)) {
      setFeedback({ message: 'Vui lòng nhập đủ 6 chữ số của mã OTP.', error: true })
      inputs.current[digits.findIndex((digit) => !digit)]?.focus()
      return
    }
    setFeedback(null)
    try {
      if (isRegister) {
        const verified = await verify.mutateAsync({ registrationId: session.registrationId, otp: digits.join('') })
        clearPendingRegistration()
        setDigits(Array(OTP_LENGTH).fill(''))
        navigate('/dang-nhap', {
          replace: true,
          state: {
            registeredUsername: verified.email,
            registrationSuccess: 'Đăng ký thành công. Hãy đăng nhập để tiếp tục.',
          },
        })
      } else {
        const verified = await verify.mutateAsync({ recoveryId: session.recoveryId, otp: digits.join('') })
        savePasswordResetGrant({ recoveryId: session.recoveryId, ...verified })
        setDigits(Array(OTP_LENGTH).fill(''))
        navigate('/dat-lai-mat-khau', { replace: true })
      }
    } catch (error) {
      handleOtpError(error)
    }
  }

  async function resendCode() {
    if (!session || expired || locked || resendExhausted || resendWait > 0 || pending) return
    setFeedback(null)
    try {
      const deadlines = await resend.mutateAsync(isRegister
        ? { registrationId: session.registrationId }
        : { recoveryId: session.recoveryId })
      const updated = { ...session, ...deadlines }
      setSession(updated)
      if (isRegister) savePendingRegistration(updated)
      setDigits(Array(OTP_LENGTH).fill(''))
      setFeedback({ message: isRegister ? 'Mã xác minh mới đã được gửi tới email của bạn.'
        : 'Yêu cầu gửi lại đã được tiếp nhận. Nếu email hợp lệ, mã mới sẽ được gửi.', error: false })
      inputs.current[0]?.focus()
    } catch (error) {
      handleOtpError(error)
    }
  }

  return (
    <div className="auth-card auth-otp-card">
      <div className="auth-recovery-icon" aria-hidden="true"><MailCheck size={27} /></div>
      <p className="auth-recovery-eyebrow">{isRegister ? 'XÁC NHẬN ĐĂNG KÝ' : 'KHÔI PHỤC TÀI KHOẢN'}</p>
      <h1 className="auth-card-title">Nhập mã xác nhận</h1>
      <p className="auth-card-sub">
        {requesting ? 'Đang xử lý yêu cầu gửi mã xác minh. Bạn có thể chờ email tại đây.'
          : isRegister ? 'Nhập mã OTP đã gửi qua email để hoàn tất đăng ký.' : 'Nhập mã OTP để tiếp tục đặt lại mật khẩu.'}
        {(email || location.state?.email) && <> Email của bạn: <strong className="auth-otp-email">{email || location.state.email}</strong>.</>}
      </p>

      {requestError && <p className="auth-otp-feedback auth-otp-feedback-error" role="alert">
        {requestError} <Link to={backTo}>Thử lại</Link>.
      </p>}

      {(!session || expired) && !requesting && !requestError &&
        <p className="auth-otp-feedback auth-otp-feedback-error" role="alert">
          {expired ? `Phiên ${isRegister ? 'đăng ký' : 'khôi phục'} đã hết hạn.` : 'Không tìm thấy phiên đang chờ xác minh.'}{' '}
          <Link to={backTo}>Bắt đầu lại</Link>.
        </p>}

      {session && !expired && otpExpired &&
        <p className="auth-otp-feedback auth-otp-feedback-error" role="status">Mã OTP đã hết hạn. Bạn có thể gửi lại mã khi hết thời gian chờ.</p>}

      <form onSubmit={submit} noValidate>
        <label className="auth-field-label" htmlFor="otp-digit-0">Mã OTP gồm 6 chữ số</label>
        <div className="auth-otp-inputs" onPaste={(event) => {
          event.preventDefault()
          fillFrom(Math.max(0, inputs.current.indexOf(document.activeElement)), event.clipboardData.getData('text'))
        }}>
          {digits.map((digit, index) => (
            <input key={index} id={'otp-digit-' + index} ref={(element) => { inputs.current[index] = element }}
              type="text" inputMode="numeric" autoComplete={index === 0 ? 'one-time-code' : 'off'}
              maxLength={OTP_LENGTH} value={digit} aria-label={'Chữ số OTP thứ ' + (index + 1)}
              disabled={requesting || pending || !session || expired || otpExpired || locked}
              onChange={(event) => {
                const value = event.target.value.replace(/\D/g, '')
                if (value) fillFrom(index, value)
                else {
                  setDigits((current) => current.map((item, position) => position === index ? '' : item))
                  setFeedback(null)
                }
              }} onKeyDown={(event) => handleKeyDown(event, index)} />
          ))}
        </div>
        <p className="auth-otp-hint">Bạn có thể dán toàn bộ mã vào ô đầu tiên.</p>
        <button className="auth-btn-main" type="submit"
          disabled={requesting || pending || !session || expired || otpExpired || locked}>
          {requesting ? 'Đang gửi yêu cầu...' : verify.isPending ? 'Đang xác minh...' : 'Kiểm tra mã'} <ArrowRight size={18} aria-hidden="true" />
        </button>
      </form>

      {feedback && <p className={feedback.error ? 'auth-otp-feedback auth-otp-feedback-error' : 'auth-otp-feedback'}
        role={feedback.error ? 'alert' : 'status'}>{feedback.message}</p>}
      <div className="auth-otp-resend">Chưa nhận được mã?{' '}
        <button type="button" onClick={resendCode}
          disabled={requesting || pending || !session || expired || locked || resendExhausted || resendWait > 0}>
          {resend.isPending ? 'Đang gửi...' : resendExhausted ? 'Đã hết lượt gửi lại'
            : resendWait > 0 ? 'Gửi lại sau ' + formatCountdown(resendWait) : 'Gửi lại mã'}
        </button>
      </div>
      <div className="auth-recovery-help"><ShieldCheck size={18} aria-hidden="true" />
        <span>{isRegister ? 'Tài khoản chỉ được tạo sau khi mã xác minh hợp lệ.' : 'Mật khẩu chỉ thay đổi sau khi bạn xác minh mã và lưu mật khẩu mới.'}</span>
      </div>
      <Link className="auth-recovery-back" to={backTo}><ArrowLeft size={17} aria-hidden="true" /> Quay lại {backLabel}</Link>
    </div>
  )
}
