export function getSessionRoles(session) {
  return (session?.roles?.length ? session.roles : [session?.role])
    .filter(Boolean).map((role) => role.toUpperCase())
}

export function hasSessionRole(session, role) {
  return getSessionRoles(session).includes(role.toUpperCase())
}

export function getLandingPath(session) {
  const primary = session?.role?.toUpperCase()
  if (primary === 'ADMIN') return '/admin'
  if (primary === 'SALES') return '/admin/tickets/pos'
  if (primary === 'LIFEGUARD') return '/admin/staff/schedule'
  if (hasSessionRole(session, 'COACH')) return '/coach/classes'
  return '/'
}
