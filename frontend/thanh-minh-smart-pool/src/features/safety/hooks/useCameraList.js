import { useQuery } from '@tanstack/react-query'
import { monitoringService } from '../services/monitoringService'

export function useCameraList(filters) {
  return useQuery({ queryKey: ['safety', 'cameras', filters],
    queryFn: ({ signal }) => monitoringService.cameras(filters, signal),
    // Không chờ 10 giây để mở lại card sau một lần loop ngắn của nguồn simulator.
    refetchInterval: query => query.state.data?.items?.some(camera => camera.fastReconnect && camera.status === 'Active') ? 1000 : 10000 })
}
