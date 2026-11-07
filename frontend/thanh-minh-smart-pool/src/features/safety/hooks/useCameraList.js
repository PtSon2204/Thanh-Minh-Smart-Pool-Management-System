import { useQuery } from '@tanstack/react-query'
import { monitoringService } from '../services/monitoringService'

export function useCameraList(filters) {
  return useQuery({ queryKey: ['safety', 'cameras', filters],
    queryFn: ({ signal }) => monitoringService.cameras(filters, signal), refetchInterval: 10000 })
}
