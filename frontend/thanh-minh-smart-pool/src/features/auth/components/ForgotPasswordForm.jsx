import { useRef } from 'react'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Link, useNavigate } from 'react-router-dom'
import { ArrowLeft, ArrowRight, Mail, ShieldCheck } from 'lucide-react'
import { forgotPasswordFormSchema } from '../types/authSchemas'
import { useRequestPasswordReset } from '../hooks/useAuthMutations'
import { clearPasswordResetGrant } from "../state/passwordResetGrant"
import { beginOtpRequest } from "../state/otpRequest"

export default function ForgotPasswordForm() {
  const navigate = useNavigate()
  const requestReset = useRequestPasswordReset()
  const submittingRef = useRef(false)
  const { register, handleSubmit, formState: { errors } } = useForm({
    resolver: zodResolver(forgotPasswordFormSchema),
    defaultValues: { email: '' },
  })

  function submit({ email }) {
    if (submittingRef.current) return
    submittingRef.current = true
    clearPasswordResetGrant()
    const trimmedEmail = email.trim()
    const requestId = beginOtpRequest('forgot-password', trimmedEmail,
      requestReset.mutateAsync({ email }).then((recovery) => ({ ...recovery, email: trimmedEmail })))
    navigate('/xac-nhan-otp/quen-mat-khau', {
      replace: true,
      state: { email: trimmedEmail, requestId },
    })
  }

  function onSubmit(event) {
    event.preventDefault()
    if (submittingRef.current) return
    void handleSubmit(submit)(event)
  }

  return (
    <div className="auth-card auth-recovery-card">
      <div className="auth-recovery-icon" aria-hidden="true"><Mail size={25} /></div>
      <p className="auth-recovery-eyebrow">KHÔI PHỤC TÀI KHOẢN</p>
      <h1 className="auth-card-title">Quên mật khẩu?</h1>
      <p className="auth-card-sub">Nhập email của bạn. Nếu tài khoản đủ điều kiện, chúng tôi sẽ gửi mã xác minh.</p>

      <form onSubmit={onSubmit} noValidate>
        <div className="auth-field-group">
          <label className="auth-field-label" htmlFor="recovery-email">Email đăng ký</label>
          <div className={`auth-field${errors.email ? ' auth-field-invalid' : ''}`}>
            <Mail size={18} aria-hidden="true" />
            <input id="recovery-email" type="email" placeholder="tenban@example.com" autoComplete="email" disabled={requestReset.isPending}
              aria-invalid={Boolean(errors.email)} aria-describedby={errors.email ? 'recovery-email-error' : undefined}
              {...register('email')} />
          </div>
          {errors.email && <p className="auth-error-text" id="recovery-email-error" role="alert">{errors.email.message}</p>}
        </div>
        <button className="auth-btn-main" type="submit" disabled={requestReset.isPending}>
          {requestReset.isPending ? 'Đang gửi yêu cầu...' : 'Gửi mã xác minh'} <ArrowRight size={18} aria-hidden="true" />
        </button>
      </form>

      <div className="auth-recovery-help"><ShieldCheck size={18} aria-hidden="true" /><span>Vì lý do bảo mật, không chia sẻ mật khẩu hoặc mã xác nhận với bất kỳ ai.</span></div>
      <Link className="auth-recovery-back" to="/dang-nhap"><ArrowLeft size={17} aria-hidden="true" /> Quay lại đăng nhập</Link>
    </div>
  )
}
