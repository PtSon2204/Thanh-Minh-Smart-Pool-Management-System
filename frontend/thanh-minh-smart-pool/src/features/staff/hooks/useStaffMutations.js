import { useMutation, useQueryClient } from '@tanstack/react-query'
import staffService from '../services/staffService'
import { STAFFS_QUERY_KEY } from './useStaffs'

function useStaffMutation(mutationFn) {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn,
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: STAFFS_QUERY_KEY })
      queryClient.invalidateQueries({ queryKey: ['staff-options'] })
    },
  })
}

export function useCreateStaff() {
  return useStaffMutation(staffService.create)
}

export function useUpdateStaff() {
  return useStaffMutation(({ userId, data }) => staffService.update(userId, data))
}

export function getStaffErrorMessage(error, fallback) {
  if (error?.response?.status === 401 || error?.response?.status === 403) return 'Bạn không có quyền thực hiện thao tác này.'
  return error?.response?.data?.message || fallback
}
