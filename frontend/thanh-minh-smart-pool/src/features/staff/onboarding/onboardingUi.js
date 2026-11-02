import dayjs from 'dayjs'

export const invitationRoleOptions = [
  { value: 'SALES', label: 'Nhân viên bán vé' },
  { value: 'LIFEGUARD', label: 'Nhân viên cứu hộ' },
  { value: 'COACH', label: 'Huấn luyện viên' },
]

export const stateLabels = {
  PENDING: 'Đang chờ', COMPLETED: 'Đã hoàn tất', CANCELLED: 'Đã hủy', EXPIRED: 'Đã hết hạn',
  DRAFT: 'Bản nháp', SUBMITTED: 'Chờ duyệt', APPROVED: 'Đã duyệt',
  CHANGES_REQUESTED: 'Yêu cầu bổ sung', REJECTED: 'Đã từ chối',
  SENT: 'Đã gửi', FAILED: 'Gửi thất bại', REQUESTED: 'Đang chờ mở chỉnh sửa', OPENED: 'Đã mở chỉnh sửa',
}

export const documentLabels = { FRONT: 'CCCD mặt trước', BACK: 'CCCD mặt sau', AVATAR: 'Ảnh đại diện' }
export const documentStatusLabels = {
  AVAILABLE: 'Có thể xem', DELETED: 'Đã xóa', EXPIRED: 'Hết thời hạn lưu',
  PENDING_DELETION: 'Không còn truy cập; chờ xóa', DELETE_PENDING: 'Không còn truy cập; chờ xóa',
  UNAVAILABLE: 'Không còn truy cập', REVOKED: 'Đã thu hồi truy cập',
}

const errorMessages = {
  EXISTING_STAFF: 'Email này đã có hồ sơ nhân viên. Hãy mở hồ sơ hiện có trong danh sách nhân viên.',
  ACCOUNT_ALREADY_EXISTS: 'Email đã thuộc một tài khoản. Không thể chuyển tài khoản hiện có thành nhân viên.',
  INVALID_TOKEN: 'Liên kết không hợp lệ hoặc đã được thay thế. Hãy yêu cầu quản trị viên gửi lại lời mời.',
  INVITATION_EXPIRED: 'Lời mời đã hết hạn. Hãy yêu cầu quản trị viên gửi lại liên kết trước khi chỉnh sửa.',
  INVITATION_CANCELLED: 'Lời mời đã bị hủy. Bạn không thể gửi hồ sơ này.',
  VERSION_CONFLICT: 'Hồ sơ đã thay đổi. Hãy tải lại dữ liệu trước khi tiếp tục.',
  STALE_VERSION: 'Phiên bản đã thay đổi. Hãy tải lại trước khi tiếp tục.',
  DOCUMENT_UNAVAILABLE: 'Ảnh không còn truy cập được. Ảnh tùy chọn không phải điều kiện duyệt hồ sơ.',
  INVALID_DOCUMENT_DELIVERY_TYPE: 'Ảnh CCCD phải dùng chế độ lưu riêng tư authenticated. Hãy liên hệ quản trị hệ thống.',
  DOCUMENT_UPLOAD_FAILED: 'Cloudinary không chấp nhận ảnh tải lên. Hãy kiểm tra ảnh và cấu hình Cloudinary; ảnh CCCD không được chuyển sang chế độ công khai.',
  NO_EMPLOYEE: 'Tài khoản này chưa có hồ sơ nhân viên.',
  EMPLOYEE_NOT_FOUND: 'Không tìm thấy hồ sơ nhân viên của tài khoản này.',
  INVALID_OR_EXPIRED_INVITATION: 'Liên kết không hợp lệ hoặc đã hết hạn. Hãy yêu cầu quản trị viên gửi lại.',
  INVALID_INVITATION_TOKEN: 'Mở lại đúng liên kết trong email lời mời.',
  INVALID_PASSWORD: 'Mật khẩu cần ít nhất 8 ký tự, có chữ và số, không quá 72 byte UTF-8.',
  ONBOARDING_KEYS_UNAVAILABLE: 'Chưa cấu hình khóa bảo vệ hồ sơ. Liên hệ quản trị hệ thống.',
  INVITATION_EXISTS: 'Email này đã có lời mời. Hãy kiểm tra danh sách và gửi lại khi cần.',
  RESEND_NOT_ALLOWED: 'Hồ sơ đang chờ duyệt nên không thể gửi lại lời mời. Hãy yêu cầu bổ sung nếu nhân viên cần chỉnh sửa.',
  IDENTITY_CONFLICT: 'Thông tin định danh đã được sử dụng. Liên hệ quản trị viên để kiểm tra.',
  PHONE_ALREADY_EXISTS: 'Số điện thoại đã được sử dụng.',
  ROLE_UNAVAILABLE: 'Vai trò trong lời mời không còn khả dụng. Quản trị viên cần kiểm tra vai trò trước khi duyệt lại.',
  INVALID_JOIN_DATE: 'Ngày vào làm phải từ hôm nay trở đi, tính theo ngày tại Việt Nam.',
}

export function onboardingError(error) {
  const data = error?.response?.data
  const code = data?.code || data?.message || data?.title
  if (errorMessages[code]) return errorMessages[code]
  if (error?.response?.status === 401 || error?.response?.status === 403) return 'Bạn không có quyền truy cập hồ sơ này. Hãy mở đúng liên kết hoặc đăng nhập tài khoản nhân viên.'
  if (!error?.response) return 'Chưa xác nhận được kết quả từ máy chủ. Không gửi lại ngay. Hãy tải lại trạng thái trước.'
  return data?.detail || data?.message || data?.title || 'Không thể thực hiện thao tác. Hãy kiểm tra thông tin và tải lại trạng thái.'
}

export function formatOnboardingDate(value, time = false) {
  if (!value) return '-'
  return time
    ? new Intl.DateTimeFormat('vi-VN', { dateStyle: 'short', timeStyle: 'short', timeZone: 'Asia/Ho_Chi_Minh' }).format(new Date(value))
    : dayjs(value).format('DD/MM/YYYY')
}

export function vietnamToday() {
  const parts = new Intl.DateTimeFormat('en-CA', { timeZone: 'Asia/Ho_Chi_Minh', year: 'numeric', month: '2-digit', day: '2-digit' }).formatToParts(new Date())
  const part = (type) => parts.find((item) => item.type === type).value
  return `${part('year')}-${part('month')}-${part('day')}`
}

export function normalizePhone(value = '') {
  return value.trim().replace(/^\+84/, '0')
}

export const phonePattern = /^(?:0|\+84)[35789][0-9]{8}$/

export function profileFromForm(values) {
  const keys = ['fullName', 'phone', 'cccdNumber', 'permanentAddress', 'temporaryAddress', 'bankName', 'bankAccountNumber', 'taxIdentifierKind', 'taxIdentifier', 'emergencyContactName', 'emergencyContactPhone', 'emergencyContactRelationship']
  const profile = Object.fromEntries(keys.map((key) => [key, (values[key] || '').trim()]))
  profile.phone = normalizePhone(profile.phone)
  profile.emergencyContactPhone = normalizePhone(profile.emergencyContactPhone)
  profile.dateOfBirth = values.dateOfBirth ? values.dateOfBirth.format('YYYY-MM-DD') : null
  return profile
}
