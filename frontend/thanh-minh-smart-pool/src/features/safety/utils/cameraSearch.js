export const normalizeCameraSearch = value => value.replace(/\s/gu, '')

export function validateCameraSearch(value) {
  const normalized = normalizeCameraSearch(value)
  if (!normalized) return 'Vui lòng nhập tên hoặc vị trí camera trước khi tìm kiếm.'
  if (normalized.length > 200) return 'Tên hoặc vị trí tìm kiếm tối đa 200 ký tự, không tính khoảng trắng.'
  if ((normalized.match(/[\p{L}\p{N}]/gu) || []).length < 2)
    return 'Vui lòng nhập ít nhất 2 chữ cái hoặc chữ số, không tính khoảng trắng và ký tự đặc biệt.'
  return null
}
