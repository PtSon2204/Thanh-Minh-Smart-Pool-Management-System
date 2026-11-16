// Accept an API origin, /hubs base, or the full hub endpoint.
export function resolveSignalRHubUrl(base, origin) {
  const url = new URL(base || origin, origin)
  const prefix = url.pathname.replace(/\/+$/, '').replace(/\/hubs(?:\/notifications)?$/i, '')
    .replace(/\/api(?:\/v\d+)?$/i, '')
  url.pathname = `${prefix}/hubs/notifications`
  url.search = ''
  url.hash = ''
  return url.href
}
