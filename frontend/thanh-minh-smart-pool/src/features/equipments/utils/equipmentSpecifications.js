function createNode(value) {
  if (value !== null && typeof value === 'object') {
    const array = Array.isArray(value)
    return {
      type: array ? 'array' : 'object',
      children: Object.entries(value).map(([name, item]) => ({ id: crypto.randomUUID(), name, node: createNode(item) })),
    }
  }
  return { type: 'value', value }
}

export function parseEquipmentMetadata(metadata) {
  if (metadata == null || metadata === '') return null
  if (typeof metadata !== 'string') return metadata
  try {
    return JSON.parse(metadata)
  } catch {
    // Older plain-text specifications remain editable without asking users to reformat them.
    return metadata
  }
}

export function specificationsFromMetadata(metadata) {
  const value = parseEquipmentMetadata(metadata)
  return value == null ? { type: 'object', children: [] } : createNode(value)
}

export function newSpecification() {
  return { id: crypto.randomUUID(), name: '', node: { type: 'value', value: '' } }
}

function emptyRow(row) {
  return !row.name.trim() && row.node.type === 'value' && String(row.node.value ?? '').trim() === ''
}

export function specificationError(node, path = '') {
  if (!node || node.type === 'value') return null
  const names = new Set()
  for (const [index, child] of node.children.entries()) {
    if (node.type === 'object' && emptyRow(child)) continue
    const name = node.type === 'array' ? `Mục ${index + 1}` : child.name.trim()
    const field = path ? `${path} / ${name || `Dòng ${index + 1}`}` : name || `Dòng ${index + 1}`
    if (node.type === 'object') {
      if (!name) return `Nhập tên thông số tại ${field}.`
      const normalized = name.normalize('NFC').toLocaleLowerCase('vi-VN')
      if (names.has(normalized)) return `Tên thông số “${name}” bị trùng${path ? ` trong ${path}` : ''}.`
      if (normalized === '_camera') return 'Tên thông số này dành riêng cho hệ thống. Vui lòng chọn tên khác.'
      names.add(normalized)
    }
    if (child.node.type === 'value' && typeof child.node.value === 'string' && !child.node.value.trim()) {
      return `Nhập giá trị cho “${field}”.`
    }
    const error = specificationError(child.node, field)
    if (error) return error
  }
  return null
}

function nodeValue(node) {
  if (node.type === 'value') return node.value
  if (node.type === 'array') return node.children.map(child => nodeValue(child.node))
  return Object.fromEntries(node.children.filter(child => !emptyRow(child)).map(child => [child.name.trim(), nodeValue(child.node)]))
}

export function specificationsToMetadata(node) {
  if (!node) return null
  const value = nodeValue(node)
  return node.type === 'object' && Object.keys(value).length === 0 ? null : value
}

export function specificationValueText(value) {
  if (value == null) return 'Chưa có giá trị'
  if (typeof value === 'boolean') return value ? 'Có' : 'Không'
  return String(value)
}
