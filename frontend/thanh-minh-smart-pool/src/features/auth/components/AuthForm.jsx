import { useRef, useState } from 'react'
import { zodResolver } from '@hookform/resolvers/zod'
import { useForm } from 'react-hook-form'
import { Link, useLocation, useNavigate } from 'react-router-dom'
import { useLogin, useRegister } from '../hooks/useAuthMutations'
import { getAuthError, loginFormSchema, registerFormSchema } from '../types/authSchemas'

function AuthField({ id, label, error, children }) {
  const errorId = `${id}-error`
  return (
    <div className="auth-field-group">
      <label className="auth-field-label" htmlFor={id}>{label}</label>
      <div className={`auth-field${error ? ' auth-field-invalid' : ''}`}>{children(errorId)}</div>
      {error && <p className="auth-error-text" id={errorId} role="alert">{error}</p>}
    </div>
  )
}

export default function AuthForm({ isRegister }) {
  const location = useLocation()
  const navigate = useNavigate()
  const [visiblePassword, setVisiblePassword] = useState(false)
  const [errorMessage, setErrorMessage] = useState('')
  const submittingRef = useRef(false)
  const login = useLogin()
  const register = useRegister()
  const pending = login.isPending || register.isPending
  const loginForm = useForm({
    resolver: zodResolver(loginFormSchema),
    defaultValues: { identifier: location.state?.registeredUsername ?? '', password: '' },
  })
  const registerForm = useForm({
    resolver: zodResolver(registerFormSchema),
    defaultValues: { username: '', phone: '', familyName: '', givenName: '', email: '', password: '', confirmPassword: '' },
  })
  const form = isRegister ? registerForm : loginForm
  const feedback = isRegister ? errorMessage : errorMessage || location.state?.registrationSuccess
  const errorFor = (name) => form.formState.errors[name]?.message

  function clearPasswords() {
    loginForm.setValue('password', '')
    registerForm.setValue('password', '')
    registerForm.setValue('confirmPassword', '')
  }

  function changeTab(nextPath) {
    if (pending) return
    clearPasswords()
    navigate(nextPath)
  }

  function applyServerErrors(fieldErrors) {
    Object.entries(fieldErrors).forEach(([field, message]) => {
      const mappedField = field === 'fullName' ? 'familyName' : field
      if (mappedField in form.getValues()) form.setError(mappedField, { type: 'server', message })
    })
  }

  async function submitLogin(values) {
    if (submittingRef.current) return
    submittingRef.current = true
    setErrorMessage('')
    try {
      const session = await login.mutateAsync(values)
      clearPasswords()
      navigate(['ADMIN', 'STAFF'].includes(session.role.toUpperCase()) ? '/admin' : '/')
    } catch (error) {
      const authError = getAuthError(error)
      applyServerErrors(authError.fieldErrors)
      setErrorMessage(authError.message)
    } finally {
      submittingRef.current = false
    }
  }

  async function submitRegister(values) {
    if (submittingRef.current) return
    submittingRef.current = true
    setErrorMessage('')
    try {
      await register.mutateAsync({
        username: values.username,
        email: values.email,
        phone: values.phone,
        password: values.password,
        confirmPassword: values.confirmPassword,
        fullName: `${values.familyName} ${values.givenName}`.trim(),
      })
      clearPasswords()
      navigate('/dang-nhap', {
        replace: true,
        state: { registeredUsername: values.username, registrationSuccess: 'Đăng ký thành công. Hãy đăng nhập để tiếp tục.' },
      })
    } catch (error) {
      const authError = getAuthError(error)
      applyServerErrors(authError.fieldErrors)
      setErrorMessage(authError.message)
    } finally {
      submittingRef.current = false
    }
  }

  function onSubmit(event) {
    event.preventDefault()
    if (submittingRef.current) return
    void form.handleSubmit(isRegister ? submitRegister : submitLogin)(event)
  }

  const nameIcon = <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="18" height="18"><circle cx="12" cy="8" r="4" /><path d="M4 20c0-4 3.6-7 8-7" /></svg>
  const mailIcon = <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="18" height="18"><rect x="2" y="4" width="20" height="16" rx="2" /><path d="m2 7 10 7 10-7" /></svg>

  return (
    <div className="auth-card" id="auth-card">
      <div className="auth-tabs">
        <button className={`auth-tab${!isRegister ? ' active' : ''}`} onClick={() => changeTab('/dang-nhap')} type="button" disabled={pending}>Đăng nhập</button>
        <button className={`auth-tab${isRegister ? ' active' : ''}`} onClick={() => changeTab('/dang-ky')} type="button" disabled={pending}>Đăng ký</button>
      </div>
      <h1 className="auth-card-title">{isRegister ? 'Tạo tài khoản' : 'Chào mừng trở lại!'}</h1>
      <p className="auth-card-sub">{isRegister ? <><b>Đăng ký</b> để đặt vé và trải nghiệm đặc quyền thành viên.</> : <><b>Đăng nhập</b> để tiếp tục trải nghiệm bể bơi Thành Minh.</>}</p>
      <form onSubmit={onSubmit} noValidate>
        {isRegister && <>
          <AuthField id="inp-username" label="Tên đăng nhập" error={errorFor('username')}>
            {(errorId) => <>{nameIcon}<input id="inp-username" placeholder="nguyen.van.an" autoComplete="username" disabled={pending} aria-invalid={Boolean(errorFor('username'))} aria-describedby={errorFor('username') ? errorId : undefined} {...registerForm.register('username')} /></>}
          </AuthField>
          <div className="auth-name-row">
            <AuthField id="inp-fname" label="Họ" error={errorFor('familyName')}>
              {(errorId) => <>{nameIcon}<input type="text" id="inp-fname" placeholder="Nguyễn" autoComplete="family-name" disabled={pending} aria-invalid={Boolean(errorFor('familyName'))} aria-describedby={errorFor('familyName') ? errorId : undefined} {...registerForm.register('familyName')} /></>}
            </AuthField>
            <AuthField id="inp-lname" label="Tên" error={errorFor('givenName')}>
              {(errorId) => <><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="18" height="18"><circle cx="12" cy="8" r="4" /><path d="M12 15c4.4 0 8 3 8 7" /></svg><input type="text" id="inp-lname" placeholder="Văn An" autoComplete="given-name" disabled={pending} aria-invalid={Boolean(errorFor('givenName'))} aria-describedby={errorFor('givenName') ? errorId : undefined} {...registerForm.register('givenName')} /></>}
            </AuthField>
          </div>
          <AuthField id="inp-phone" label="Số điện thoại" error={errorFor('phone')}>
            {(errorId) => <><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="18" height="18"><path d="M22 16.92v3a2 2 0 0 1-2.18 2 19.8 19.8 0 0 1-8.63-3.07 19.5 19.5 0 0 1-6-6A19.8 19.8 0 0 1 2.12 4.18 2 2 0 0 1 4.11 2h3a2 2 0 0 1 2 1.72c.12.9.33 1.78.62 2.63a2 2 0 0 1-.45 2.11L7.99 9.75a16 16 0 0 0 6 6l1.29-1.29a2 2 0 0 1 2.11-.45c.85.29 1.73.5 2.63.62A2 2 0 0 1 22 16.92z" /></svg><input id="inp-phone" type="tel" placeholder="0912345678" autoComplete="tel" disabled={pending} aria-invalid={Boolean(errorFor('phone'))} aria-describedby={errorFor('phone') ? errorId : undefined} {...registerForm.register('phone')} /></>}
          </AuthField>
        </>}
        <AuthField id="inp-email" label={isRegister ? 'Email' : 'Tên đăng nhập, email hoặc số điện thoại'} error={errorFor(isRegister ? 'email' : 'identifier')}>
          {(errorId) => <>{mailIcon}<input type={isRegister ? 'email' : 'text'} id="inp-email" placeholder={isRegister ? 'Eg. johndoe@gmail.com' : 'Tên đăng nhập, email hoặc số điện thoại'} autoComplete={isRegister ? 'email' : 'username'} aria-label={isRegister ? 'Email' : 'Tên đăng nhập, email hoặc số điện thoại'} disabled={pending} aria-invalid={Boolean(errorFor(isRegister ? 'email' : 'identifier'))} aria-describedby={errorFor(isRegister ? 'email' : 'identifier') ? errorId : undefined} {...(isRegister ? registerForm.register('email') : loginForm.register('identifier'))} /></>}
        </AuthField>
        <AuthField id="inp-pw" label="Mật khẩu" error={errorFor('password')}>
          {(errorId) => <><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="18" height="18"><rect x="3" y="11" width="18" height="11" rx="2" /><path d="M7 11V7a5 5 0 0 1 10 0v4" /></svg><input type={visiblePassword ? 'text' : 'password'} id="inp-pw" placeholder="Mật khẩu" autoComplete={isRegister ? 'new-password' : 'current-password'} disabled={pending} aria-invalid={Boolean(errorFor('password'))} aria-describedby={errorFor('password') ? errorId : undefined} {...form.register('password')} /><button className="auth-eye-btn" type="button" onClick={() => setVisiblePassword((current) => !current)} aria-label="Hiện/ẩn mật khẩu" disabled={pending}>{visiblePassword ? <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="18" height="18"><path d="M17.94 17.94A10.07 10.07 0 0 1 12 20c-7 0-11-8-11-8a18.45 18.45 0 0 1 5.06-5.94M9.9 4.24A9.12 9.12 0 0 1 12 4c7 0 11 8 11 8a18.5 18.5 0 0 1-2.16 3.19m-6.72-1.07a3 3 0 1 1-4.24-4.24" /><line x1="1" y1="1" x2="23" y2="23" stroke="currentColor" strokeWidth="2" /></svg> : <svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="18" height="18"><path d="M1 12s4-8 11-8 11 8 11 8-4 8-11 8-11-8-11-8z" /><circle cx="12" cy="12" r="3" /></svg>}</button></>}
        </AuthField>
        {isRegister && <AuthField id="inp-pw2" label="Xác nhận mật khẩu" error={errorFor('confirmPassword')}>
          {(errorId) => <><svg viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="2" width="18" height="18"><path d="M9 12l2 2 4-4" /><rect x="3" y="11" width="18" height="11" rx="2" /><path d="M7 11V7a5 5 0 0 1 10 0v4" /></svg><input type="password" id="inp-pw2" placeholder="Nhập lại mật khẩu" autoComplete="new-password" disabled={pending} aria-invalid={Boolean(errorFor('confirmPassword'))} aria-describedby={errorFor('confirmPassword') ? errorId : undefined} {...registerForm.register('confirmPassword')} /></>}
        </AuthField>}
        {!isRegister && <div className="auth-forgot-row"><Link to="#" className="auth-forgot-link">Quên mật khẩu?</Link></div>}
        {feedback && <p className={`auth-feedback${errorMessage ? ' auth-feedback-error' : ''}`} role="alert">{feedback}</p>}
        <button className="auth-btn-main" id="loginBtn" type="submit" disabled={pending}><span>{pending ? 'Đang xử lý...' : isRegister ? 'Đăng ký' : 'Đăng nhập'}</span><svg viewBox="0 0 22 22" fill="none" width="18" height="18"><path d="M3 11h15.4M11 3.3l7.7 7.7-7.7 7.7" stroke="#fff" strokeWidth="2.6" strokeLinecap="round" strokeLinejoin="round" /></svg></button>
      </form>
      <div className="auth-divider"><i /><b>HOẶC</b><i /></div>
      <button className="auth-btn-google" id="gBtn" type="button"><svg viewBox="0 0 48 48" width="22" height="22"><path fill="#EA4335" d="M24 9.5c3.54 0 6.71 1.22 9.21 3.6l6.85-6.85C35.9 2.38 30.47 0 24 0 14.62 0 6.51 5.38 2.56 13.22l7.98 6.19C12.43 13.72 17.74 9.5 24 9.5z" /><path fill="#4285F4" d="M46.98 24.55c0-1.57-.15-3.09-.38-4.55H24v9.02h12.94c-.58 2.96-2.26 5.48-4.78 7.18l7.73 6c4.51-4.18 7.09-10.36 7.09-17.65z" /><path fill="#FBBC05" d="M10.53 28.59c-.48-1.45-.76-2.99-.76-4.59s.27-3.14.76-4.59l-7.98-6.19C.92 16.46 0 20.12 0 24c0 3.88.92 7.54 2.56 10.78l7.97-6.19z" /><path fill="#34A853" d="M24 48c6.48 0 11.93-2.13 15.89-5.81l-7.73-6c-2.18 1.48-4.97 2.31-8.16 2.31-6.26 0-11.57-4.22-13.47-9.91l-7.98 6.19C6.51 42.62 14.62 48 24 48z" /></svg><span>{isRegister ? 'Đăng ký với Google' : 'Đăng nhập với Google'}</span></button>
      <p className="auth-footer-text">{isRegister ? 'Đã có tài khoản? ' : 'Chưa có tài khoản? '}<button type="button" className="auth-footer-link" onClick={() => changeTab(isRegister ? '/dang-nhap' : '/dang-ky')} disabled={pending}>{isRegister ? 'Đăng nhập' : 'Đăng ký miễn phí'}</button></p>
      {isRegister && <p className="auth-terms-text">Bằng cách đăng ký, bạn đồng ý với <Link to="#">Điều khoản dịch vụ</Link> và <Link to="#">Chính sách bảo mật</Link>.</p>}
    </div>
  )
}
