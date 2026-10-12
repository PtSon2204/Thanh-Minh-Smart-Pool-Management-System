import { useQuery } from '@tanstack/react-query'
import staffService from '../services/staffService'

export const STAFFS_QUERY_KEY = ['staffs']

export function useStaffs(params, enabled = true) {
  return useQuery({
    queryKey: [...STAFFS_QUERY_KEY, params],
    queryFn: () => staffService.getAll(params),
    enabled,
  })
}

export function useStaffOptions(enabled) {
  return useQuery({
    queryKey: ['staff-options'],
    queryFn: staffService.getOptions,
    enabled,
  })
}

export function useStaffSchedule(employeeId, params, enabled) {
  return useQuery({
    queryKey: ['staff-schedule', employeeId, params],
    queryFn: () => staffService.getSchedule({ ...params, employeeId }),
    enabled: Boolean(enabled && employeeId),
  })
}

export function useStaffSchedulePage({ scope, sessionUserId, params, enabled }) {
  const isAllScope = scope === 'all'
  const queryParams = isAllScope ? params : { pageIndex: params.pageIndex, pageSize: params.pageSize, date: params.date }
  return useQuery({
    queryKey: ['staff-schedule-page', scope, sessionUserId, queryParams],
    queryFn: () => isAllScope ? staffService.getSchedule(queryParams) : staffService.getMySchedule(queryParams),
    enabled: Boolean(enabled && sessionUserId),
  })
}

export function useStaffSalaries(employeeId, params, enabled) {
  return useQuery({
    queryKey: ['staff-salaries', employeeId, params],
    queryFn: () => staffService.getSalaries({ ...params, employeeId }),
    enabled: Boolean(enabled && employeeId),
  })
}
