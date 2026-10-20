import { useEffect, useState } from 'react'
import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { hasValidAccessToken, hasValidRefreshToken, useAuthStore } from '../features/auth/store/authStore'
import { refreshSession } from '../services/apiClient'

export default function ProtectedRoute({ allowedRoles, redirectTo, children }) {
  const session = useAuthStore((state) => state.session)
  const [refreshFailed, setRefreshFailed] = useState(false)
  const location = useLocation()
  const unauthenticatedRedirectTo = redirectTo ?? '/dang-nhap'
  const forbiddenRedirectTo = redirectTo ?? '/401'

  const hasToken = hasValidAccessToken(session) || hasValidRefreshToken(session)

  useEffect(() => {
    if (session && !hasToken) {
      useAuthStore.getState().setSession(null)
    } else if (session && !hasValidAccessToken(session) && hasValidRefreshToken(session)) {
      let active = true
      void refreshSession()
        .then(() => { if (active) setRefreshFailed(false) })
        .catch(() => { if (active) setRefreshFailed(true) })
      return () => { active = false }
    }
  }, [session, hasToken])

  if (!hasToken) {
    return (
      <Navigate
        to={unauthenticatedRedirectTo}
        replace
        state={{
          from: location,
          message: 'Phiên đăng nhập đã hết hạn hoặc bạn chưa đăng nhập.',
        }}
      />
    )
  }

  if (!hasValidAccessToken(session)) {
    return (
      <div role="status">
        {refreshFailed ? (
          <>
            <p>Không thể làm mới phiên đăng nhập.</p>
            <button type="button" onClick={() => {
              setRefreshFailed(false)
              void refreshSession().catch(() => setRefreshFailed(true))
            }}>Thử lại</button>
          </>
        ) : <p>Đang làm mới phiên đăng nhập...</p>}
      </div>
    )
  }

  // 2. Kiểm tra vai trò (Role-based access control)
  if (allowedRoles && allowedRoles.length > 0) {
    const userRole = session.role ? session.role.toUpperCase() : ''
    const normalizedAllowedRoles = allowedRoles.map((r) => r.toUpperCase())

    if (!normalizedAllowedRoles.includes(userRole)) {
      return (
        <Navigate
          to={forbiddenRedirectTo}
          replace
          state={{
            from: location,
            message: 'Tài khoản của bạn không có quyền truy cập trang này.',
          }}
        />
      )
    }
  }

  return children ? children : <Outlet />
}
