import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { maintenanceService } from '../services/maintenanceService'

export function useMaintenanceList(params) {
  return useQuery({ queryKey: ['maintenance', 'list', params], queryFn: ({ signal }) => maintenanceService.list(params, signal), refetchInterval: 60000 })
}

export function useMaintenanceOptions(enabled = true) {
  return useQuery({ queryKey: ['maintenance', 'options'], queryFn: ({ signal }) => maintenanceService.options(signal), enabled, staleTime: 0 })
}

export function useMaintenanceHistory(params) {
  return useQuery({ queryKey: ['maintenance', 'history', params], queryFn: ({ signal }) => maintenanceService.history(params, signal) })
}

export function useMaintenanceMutation() {
  const client = useQueryClient()
  return useMutation({
    retry: false,
    mutationFn: ({ action, id, payload }) => {
      if (action === 'create') return maintenanceService.create(payload)
      if (action === 'update') return maintenanceService.update(id, payload)
      if (action === 'complete') return maintenanceService.complete(id, payload)
      return maintenanceService.changeState(id, action)
    },
    onSuccess: async () => {
      await Promise.all([
        client.invalidateQueries({ queryKey: ['maintenance'] }),
        client.invalidateQueries({ queryKey: ['equipments'] }),
      ])
    },
  })
}
