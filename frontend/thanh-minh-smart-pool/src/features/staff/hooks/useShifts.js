import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import shiftService from '../services/shiftService'

export const SHIFTS_QUERY_KEY = ['shifts']

export function useShifts(params, enabled) {
  return useQuery({
    queryKey: [...SHIFTS_QUERY_KEY, params],
    queryFn: () => shiftService.getAll(params),
    enabled,
  })
}

export function useActiveShifts(enabled) {
  return useQuery({
    queryKey: [...SHIFTS_QUERY_KEY, 'active', 'complete'],
    queryFn: ({ signal }) => shiftService.getAllActive(signal),
    enabled,
  })
}

export function useCreateShift() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: shiftService.create,
    onSuccess: () => queryClient.invalidateQueries({ queryKey: SHIFTS_QUERY_KEY }),
  })
}

export function useUpdateShift() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: ({ id, data }) => shiftService.update(id, data),
    retry: false,
    onSuccess: () => Promise.all([
      queryClient.invalidateQueries({ queryKey: SHIFTS_QUERY_KEY }),
      queryClient.invalidateQueries({ queryKey: ['staff-schedule-page'] }),
      queryClient.invalidateQueries({ queryKey: ['staff-schedule'] }),
    ]),
  })
}
