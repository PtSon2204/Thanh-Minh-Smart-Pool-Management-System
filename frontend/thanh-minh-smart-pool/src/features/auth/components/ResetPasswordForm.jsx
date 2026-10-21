import { useRef, useState } from 'react'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Link, useNavigate } from 'react-router-dom'
import { ArrowLeft, ArrowRight, Eye, EyeOff, LockKeyhole, ShieldCheck } from 'lucide-react'
import { useCompletePasswordReset } from '../hooks/useAuthMutations'
import { clearPasswordResetGrant, readPasswordResetGrant } from "../state/passwordResetGrant"
import { getAuthError, resetPasswordFormSchema } from '../types/authSchemas'

const fields = [
  { name: 'newPassword', label: 'Mật khẩu mới', placeholder: 'Nhập mật khẩu mới' },
  { name: 'confirmPassword', label: 'Xác nhận mật khẩu mới', placeholder: 'Nhập lại mật khẩu mới' },
]

export default function ResetPasswordForm() {
  const navigate = useNavigate()
  const complete = useCompletePasswordReset()
  const submittingRef = useRef(false)
  const [visible, setVisible] = useState({})
  const [feedback, setFeedback] = useState('')
  const [unavailable, setUnavailable] = useState(() => !readPasswordResetGrant())
  const { register, handleSubmit, setError, reset, formState: { errors } } = useForm({
    resolver: zodResolver(resetPasswordFormSchema),
    defaultValues: { newPassword: '', confirmPassword: '' },
  })

  async function submit(values) {
    if (submittingRef.current) return
    const grant = readPasswordResetGrant()
    if (!grant) {
      setUnavailable(true)
      reset()
      return
    }
    submittingRef.current = true
    setFeedback('')
    try {
      await complete.mutateAsync({
        recoveryId: grant.recoveryId,
        resetToken: grant.resetToken,
        newPassword: values.newPassword,
        confirmPassword: values.confirmPassword,
      })
      clearPasswordResetGrant()
      reset()
      navigate('/dang-nhap', {
        replace: true,
        state: { resetSuccess: 'Mật khẩu đã được đặt lại. Hãy đăng nhập bằng mật khẩu mới.' },
      })
    } catch (error) {
      const authError = getAuthError(error)
      for (const field of ['newPassword', 'confirmPassword']) {
        if (authError.fieldErrors[field]) setError(field, { type: 'server', message: authError.fieldErrors[field] })
      }
      const code = error?.response?.data?.code
      if (['INVALID_RESET_TOKEN', 'RESET_TOKEN_EXPIRED', 'RECOVERY_UNAVAILABLE', 'PASSWORD_CHANGED'].includes(code)) {
        clearPasswordResetGrant()
        setUnavailable(true)
        reset()
      }
      setFeedback(authError.message)
    } finally {
      submittingRef.current = false
    }
  }

  function onSubmit(event) {
    event.preventDefault()
    if (submittingRef.current) return
    void handleSubmit(submit)(event)
  }

  return (
    <div className="auth-card auth-recovery-card auth-reset-card">
      <div className="auth-recovery-icon" aria-hidden="true"><LockKeyhole size={25} /></div>
      <p className="auth-recovery-eyebrow">KHÔI PHỤC TÀI KHOẢN</p>
      <h1 className="auth-card-title">Đặt mật khẩu mới</h1>
      <p className="auth-card-sub">Chọn mật khẩu mới để hoàn tất khôi phục tài khoản.</p>

      {unavailable ? (
        <p className="auth-otp-feedback auth-otp-feedback-error" role="alert">
          {feedback || 'Phiên đặt lại mật khẩu không còn hiệu lực. Vui lòng bắt đầu lại.'}{' '}
          <Link to="/quen-mat-khau" onClick={clearPasswordResetGrant}>Bắt đầu lại</Link>.
        </p>
      ) : (
        <form onSubmit={onSubmit} noValidate>
          {fields.map(({ name, label, placeholder }) => (
            <div className="auth-field-group" key={name}>
              <label className="auth-field-label" htmlFor={`reset-${name}`}>{label}</label>
              <div className={`auth-field${errors[name] ? ' auth-field-invalid' : ''}`}>
                <LockKeyhole size={18} aria-hidden="true" />
                <input id={`reset-${name}`} type={visible[name] ? 'text' : 'password'}
                  autoComplete="new-password" placeholder={placeholder} disabled={complete.isPending}
                  aria-invalid={Boolean(errors[name])}
                  aria-describedby={errors[name] ? `reset-${name}-error` : undefined}
                  {...register(name, { onChange: () => setFeedback('') })} />
                <button className="auth-eye-btn" type="button" disabled={complete.isPending}
                  onClick={() => setVisible((current) => ({ ...current, [name]: !current[name] }))}
                  aria-label={visible[name] ? `Ẩn ${label.toLowerCase()}` : `Hiện ${label.toLowerCase()}`}>
                  {visible[name] ? <EyeOff size={18} /> : <Eye size={18} />}
                </button>
              </div>
              {errors[name] && <p className="auth-error-text" id={`reset-${name}-error`} role="alert">{errors[name].message}</p>}
            </div>
          ))}
          <div className="auth-recovery-help"><ShieldCheck size={18} aria-hidden="true" />
            <span>Mật khẩu cần từ 8 đến 128 ký tự, có ít nhất một chữ cái và một chữ số.</span>
          </div>
          {feedback && <p className="auth-feedback auth-feedback-error" role="alert">{feedback}</p>}
          <button className="auth-btn-main" type="submit" disabled={complete.isPending}>
            {complete.isPending ? 'Đang lưu mật khẩu...' : 'Lưu mật khẩu mới'} <ArrowRight size={18} aria-hidden="true" />
          </button>
        </form>
      )}
      <Link className="auth-recovery-back" to="/quen-mat-khau" onClick={clearPasswordResetGrant}>
        <ArrowLeft size={17} aria-hidden="true" /> Bắt đầu lại
      </Link>
    </div>
  )
}
