import { useMutation, useQueryClient } from '@tanstack/react-query'
import userService from '../services/userService'
import { getUserError } from '../userManagement'

export function useSaveUser() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: async ({ values, record }) => {
      const profile = {
        email: values.email.trim(),
        phone: values.phone.trim(),
        fullName: values.fullName.trim(),
        dateOfBirth: values.dateOfBirth || null,
        address: values.address?.trim() || null,
      }
      if (!record) {
        return userService.create({
          ...profile,
          username: values.username.trim(),
          password: values.password,
          confirmPassword: values.confirmPassword,
          role: values.role,
        })
      }

      await userService.update(record.id, profile)
      if (values.role !== record.role) {
        try {
          await userService.changeRole(record.id, values.role)
        } catch (error) {
          throw new Error(`Đã lưu thông tin tài khoản nhưng chưa đổi được vai trò: ${getUserError(error)}`, { cause: error })
        }
      }
    },
    onSettled: () => queryClient.invalidateQueries({ queryKey: ['users'] }),
  })
}

export function useChangeUserStatus() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, status }) => userService.changeStatus(id, status),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: ['users'] }),
  })
}
