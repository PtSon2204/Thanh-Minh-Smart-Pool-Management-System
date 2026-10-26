import { z } from 'zod'

export const passwordSchema = z
  .string()
  .min(8, 'Mật khẩu phải có ít nhất 8 ký tự.')
  .max(128, 'Mật khẩu không được vượt quá 128 ký tự.')
  .regex(/\p{L}/u, 'Mật khẩu phải có ít nhất một chữ cái.')
  .regex(/\p{Nd}/u, 'Mật khẩu phải có ít nhất một chữ số.')

export const loginFormSchema = z.object({
  identifier: z.string().trim().min(1, 'Nhập tên đăng nhập, email hoặc số điện thoại.').max(150, 'Thông tin đăng nhập không được vượt quá 150 ký tự.'),
  password: z.string().min(1, 'Nhập mật khẩu.'),
})

export const registerFormSchema = z
  .object({
    username: z
      .string()
      .trim()
      .min(3, 'Tên đăng nhập phải có ít nhất 3 ký tự.')
      .max(100, 'Tên đăng nhập không được vượt quá 100 ký tự.')
      .regex(/^[A-Za-z0-9](?:[A-Za-z0-9._]*[A-Za-z0-9])?$/, 'Tên đăng nhập chỉ dùng chữ cái, số, dấu chấm và gạch dưới.'),
    phone: z.string().trim().regex(/^(?:0|\+84)[35789]\d{8}$/, 'Số điện thoại Việt Nam không hợp lệ.'),
    familyName: z.string().trim().min(1, 'Nhập họ.'),
    givenName: z.string().trim().min(1, 'Nhập tên.'),
    email: z.string().trim().max(150, 'Email không được vượt quá 150 ký tự.').email('Email không hợp lệ.').refine((value) => !value.includes('..'), 'Email không được có hai dấu chấm liền nhau.'),
    password: passwordSchema,
    confirmPassword: z.string().min(1, 'Xác nhận mật khẩu.'),
  })
  .refine((values) => values.password === values.confirmPassword, {
    message: 'Mật khẩu xác nhận không khớp.',
    path: ['confirmPassword'],
  })
  .refine((values) => `${values.familyName} ${values.givenName}`.trim().length <= 255, {
    message: 'Họ và tên không được vượt quá 255 ký tự.',
    path: ['givenName'],
  })

export const googleRegisterFormSchema = z.object({
  username: z.string().trim().min(3, 'Tên đăng nhập phải có ít nhất 3 ký tự.')
    .max(100, 'Tên đăng nhập không được vượt quá 100 ký tự.')
    .regex(/^[A-Za-z0-9](?:[A-Za-z0-9._]*[A-Za-z0-9])?$/,
      'Tên đăng nhập chỉ dùng chữ cái, số, dấu chấm và gạch dưới.'),
  phone: z.string().trim().regex(/^(?:0|\+84)[35789]\d{8}$/,
    'Số điện thoại Việt Nam không hợp lệ.'),
  fullName: z.string().trim().min(1, 'Nhập họ và tên.')
    .max(255, 'Họ và tên không được vượt quá 255 ký tự.'),
})

export const forgotPasswordFormSchema = z.object({
  email: z.string().trim().max(150, 'Email không được vượt quá 150 ký tự.').email('Email không hợp lệ.'),
})

export const changePasswordFormSchema = z.object({
  currentPassword: z.string().min(1, 'Nhập mật khẩu hiện tại.'),
  newPassword: passwordSchema,
  confirmPassword: z.string().min(1, 'Xác nhận mật khẩu mới.'),
}).refine((values) => values.newPassword !== values.currentPassword, {
  message: 'Mật khẩu mới phải khác mật khẩu hiện tại.',
  path: ['newPassword'],
}).refine((values) => values.newPassword === values.confirmPassword, {
  message: 'Mật khẩu xác nhận không khớp.',
  path: ['confirmPassword'],
})

export const resetPasswordFormSchema = z.object({
  newPassword: passwordSchema,
  confirmPassword: z.string().min(1, 'Xác nhận mật khẩu mới.'),
}).refine((values) => values.newPassword === values.confirmPassword, {
  message: 'Mật khẩu xác nhận không khớp.',
  path: ['confirmPassword'],
})

export const loginResponseSchema = z.object({
  id: z.string().uuid(),
  username: z.string(),
  role: z.string().min(1),
  accessToken: z.string().min(1),
  expiresAtUtc: z.string().refine((value) => Date.parse(value) > Date.now(), 'Thời hạn phiên không hợp lệ.'),
  refreshToken: z.string().min(1),
  refreshTokenExpiresAtUtc: z.string().refine((value) => Date.parse(value) > Date.now(), 'Thời hạn đăng nhập không hợp lệ.'),
})

export const refreshResponseSchema = loginResponseSchema

const registrationDeadlinesSchema = z.object({
  otpExpiresAtUtc: z.string().refine((value) => !Number.isNaN(Date.parse(value)), 'Thời hạn OTP không hợp lệ.'),
  registrationExpiresAtUtc: z.string().refine((value) => !Number.isNaN(Date.parse(value)), 'Thời hạn đăng ký không hợp lệ.'),
  resendAvailableAtUtc: z.string().refine((value) => !Number.isNaN(Date.parse(value)), 'Thời gian gửi lại không hợp lệ.'),
})

export const registerPendingResponseSchema = registrationDeadlinesSchema.extend({
  registrationId: z.string().uuid(),
})

export const resendRegistrationResponseSchema = registrationDeadlinesSchema

export const verifiedRegistrationResponseSchema = z.object({
  id: z.string().uuid(),
  username: z.string().min(1),
})

const recoveryDeadlinesSchema = z.object({
  otpExpiresAtUtc: z.string().refine((value) => !Number.isNaN(Date.parse(value)), 'Thời hạn OTP không hợp lệ.'),
  recoveryExpiresAtUtc: z.string().refine((value) => !Number.isNaN(Date.parse(value)), 'Thời hạn khôi phục không hợp lệ.'),
  resendAvailableAtUtc: z.string().refine((value) => !Number.isNaN(Date.parse(value)), 'Thời gian gửi lại không hợp lệ.'),
})

export const requestPasswordResetResponseSchema = recoveryDeadlinesSchema.extend({
  recoveryId: z.string().uuid(),
})

export const resendPasswordResetResponseSchema = recoveryDeadlinesSchema

export const verifyPasswordResetResponseSchema = z.object({
  resetToken: z.string().regex(/^[0-9A-Fa-f]{64}$/, 'Reset token không hợp lệ.'),
  resetTokenExpiresAtUtc: z.string().refine((value) => Date.parse(value) > Date.now(), 'Reset token đã hết hạn.'),
})

export function getAuthError(error) {
  if (error instanceof z.ZodError) {
    return { message: 'Phản hồi máy chủ không hợp lệ. Vui lòng thử lại.', fieldErrors: {} }
  }
  if (error instanceof Error && !error?.response) {
    if (error.message.startsWith('Phản hồi ')) return { message: error.message, fieldErrors: {} }
  }
  const response = error?.response
  const data = response?.data
  const fieldErrors = Object.fromEntries(
    Object.entries(data?.errors ?? {}).map(([field, messages]) => [
      field.charAt(0).toLowerCase() + field.slice(1),
      Array.isArray(messages) ? messages[0] : messages,
    ]),
  )

  if (response?.status === 401) {
    return { message: data?.title || 'Tên đăng nhập hoặc mật khẩu không đúng.', fieldErrors }
  }

  if (response?.status === 409) {
    return { message: data?.detail || data?.title || 'Thông tin đăng ký đã được sử dụng.', fieldErrors }
  }

  if (response?.status) {
    return { message: data?.detail || data?.title || 'Không thể xử lý yêu cầu. Vui lòng thử lại.', fieldErrors }
  }

  return { message: 'Không thể kết nối đến máy chủ. Vui lòng kiểm tra mạng và thử lại.', fieldErrors: {} }
}
