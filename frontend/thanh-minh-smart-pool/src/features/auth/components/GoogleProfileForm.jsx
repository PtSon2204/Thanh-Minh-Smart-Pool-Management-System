import { useState } from 'react'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { useGoogleRegister } from '../hooks/useAuthMutations'
import { getAuthError, googleRegisterFormSchema } from '../types/authSchemas'

export default function GoogleProfileForm({ credential, initialValues, onSuccess, onCancel }) {
  const googleRegister = useGoogleRegister()
  const [errorMessage, setErrorMessage] = useState('')
  const { register, handleSubmit, setError, formState: { errors } } = useForm({
    resolver: zodResolver(googleRegisterFormSchema),
    defaultValues: initialValues,
  })

  async function submit(values) {
    setErrorMessage('')
    try {
      const session = await googleRegister.mutateAsync({ credential, ...values })
      onSuccess(session)
    } catch (error) {
      const result = getAuthError(error)
      Object.entries(result.fieldErrors).forEach(([field, message]) => {
        if (field in values) setError(field, { type: 'server', message })
      })
      setErrorMessage(result.message)
    }
  }

  return (
    <div className="auth-card" id="auth-card">
      <h1 className="auth-card-title">Hoàn tất đăng ký Google</h1>
      <p className="auth-card-sub">Thêm thông tin để tạo tài khoản khách hàng.</p>
      <form onSubmit={handleSubmit(submit)} noValidate>
        <div className="auth-field-group">
          <label className="auth-field-label" htmlFor="google-username">Tên đăng nhập</label>
          <div className={`auth-field${errors.username ? ' auth-field-invalid' : ''}`}>
            <input id="google-username" autoComplete="username" disabled={googleRegister.isPending}
              aria-invalid={Boolean(errors.username)} {...register('username')} />
          </div>
          {errors.username && <p className="auth-error-text" role="alert">{errors.username.message}</p>}
        </div>
        <div className="auth-field-group">
          <label className="auth-field-label" htmlFor="google-full-name">Họ và tên</label>
          <div className={`auth-field${errors.fullName ? ' auth-field-invalid' : ''}`}>
            <input id="google-full-name" autoComplete="name" disabled={googleRegister.isPending}
              aria-invalid={Boolean(errors.fullName)} {...register('fullName')} />
          </div>
          {errors.fullName && <p className="auth-error-text" role="alert">{errors.fullName.message}</p>}
        </div>
        <div className="auth-field-group">
          <label className="auth-field-label" htmlFor="google-phone">Số điện thoại</label>
          <div className={`auth-field${errors.phone ? ' auth-field-invalid' : ''}`}>
            <input id="google-phone" type="tel" autoComplete="tel" disabled={googleRegister.isPending}
              aria-invalid={Boolean(errors.phone)} {...register('phone')} />
          </div>
          {errors.phone && <p className="auth-error-text" role="alert">{errors.phone.message}</p>}
        </div>
        {errorMessage && <p className="auth-feedback auth-feedback-error" role="alert">{errorMessage}</p>}
        <button className="auth-btn-main" type="submit" disabled={googleRegister.isPending}>
          {googleRegister.isPending ? 'Đang tạo tài khoản...' : 'Tạo tài khoản với Google'}
        </button>
      </form>
      <button className="auth-footer-link" type="button" onClick={onCancel}
        disabled={googleRegister.isPending}>Chọn tài khoản Google khác</button>
    </div>
  )
}
