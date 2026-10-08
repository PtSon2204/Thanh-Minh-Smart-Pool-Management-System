import { useQuery } from '@tanstack/react-query'
import userService from '../services/userService'

export function useUsers(params) {
  return useQuery({ queryKey: ['users', params], queryFn: () => userService.getAll(params) })
}

export function useUserDetail(id) {
  return useQuery({ queryKey: ['users', 'detail', id], queryFn: () => userService.getById(id), enabled: Boolean(id) })
}
