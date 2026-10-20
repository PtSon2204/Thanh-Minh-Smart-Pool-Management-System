const requests = new Map()
let nextId = 0

export function beginOtpRequest(flow, email, promise) {
  const id = ++nextId
  const result = promise.then(
    (session) => ({ session }),
    (error) => ({ error }),
  )
  requests.set(flow, { id, email, result })
  return id
}

export function getOtpRequest(flow, id) {
  const request = requests.get(flow)
  return request?.id === id ? request : null
}
