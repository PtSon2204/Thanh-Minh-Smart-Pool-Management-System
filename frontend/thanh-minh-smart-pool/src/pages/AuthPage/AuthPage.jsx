import { useLocation } from 'react-router-dom'
import AuthBranding from '../../features/auth/components/AuthBranding'
import AuthForm from '../../features/auth/components/AuthForm'
import AuthPhoto from '../../features/auth/components/AuthPhoto'
import './AuthPage.css'

export default function AuthPage() {
  const location = useLocation()
  const isRegister = location.pathname === '/dang-ky'

  return (
    <div className="auth-stage">
      <section className="auth-pane">
        <AuthBranding />
        <AuthForm key={location.pathname} isRegister={isRegister} />
      </section>
      <AuthPhoto />
    </div>
  )
}
