function storageKey(userId) { return `swim-class-draft:${userId}` }

export function readClassDraft(userId) {
  if (!userId) return null
  try {
    const draft = JSON.parse(sessionStorage.getItem(storageKey(userId)))
    return draft?.id && draft?.name ? { ...draft, sessions: draft.sessions || [] } : null
  } catch {
    return null
  }
}

export function saveClassDraft(userId, draft) {
  if (userId) sessionStorage.setItem(storageKey(userId), JSON.stringify(draft))
}

export function clearClassDraft(userId) {
  if (userId) sessionStorage.removeItem(storageKey(userId))
}
