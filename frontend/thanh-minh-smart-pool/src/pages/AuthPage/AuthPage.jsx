import { useLocation } from 'react-router-dom'
import AuthBranding from '../../features/auth/components/AuthBranding'
import AuthForm from '../../features/auth/components/AuthForm'
import ForgotPasswordForm from '../../features/auth/components/ForgotPasswordForm'
import ResetPasswordForm from '../../features/auth/components/ResetPasswordForm'
import OtpForm from '../../features/auth/components/OtpForm'
import AuthPhoto from '../../features/auth/components/AuthPhoto'
import './AuthPage.css'

export default function AuthPage() {
  const location = useLocation()
  const isRegister = location.pathname === '/dang-ky'
  const isForgotPassword = location.pathname === '/quen-mat-khau'
  const isResetPassword = location.pathname === '/dat-lai-mat-khau'
  const otpFlow = location.pathname === '/xac-nhan-otp/dang-ky' ? 'register'
    : location.pathname === '/xac-nhan-otp/quen-mat-khau' ? 'forgot-password' : null

  return (
    <div className="auth-stage">
      <section className="auth-pane">
        <AuthBranding />
        {otpFlow ? <OtpForm key={location.pathname} flow={otpFlow} />
          : isForgotPassword ? <ForgotPasswordForm />
            : isResetPassword ? <ResetPasswordForm />
            : <AuthForm key={location.pathname} isRegister={isRegister} />}
      </section>
      <AuthPhoto />
    </div>
  )
}
