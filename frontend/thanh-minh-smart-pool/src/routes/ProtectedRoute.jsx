import { useEffect } from 'react'
import { Navigate, Outlet, useLocation } from 'react-router-dom'
import { useAuthStore } from '../features/auth/store/authStore'

export default function ProtectedRoute({ allowedRoles, redirectTo, children }) {
  const session = useAuthStore((state) => state.session)
  const location = useLocation()
  const unauthenticatedRedirectTo = redirectTo ?? '/dang-nhap'
  const forbiddenRedirectTo = redirectTo ?? '/401'

  // 1. Kiểm tra trạng thái đăng nhập
  const hasToken = Boolean(session?.accessToken)

  useEffect(() => {
    if (session?.expiresAtUtc && Date.parse(session.expiresAtUtc) <= Date.now()) {
      useAuthStore.getState().setSession(null)
    }
  }, [session])

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
