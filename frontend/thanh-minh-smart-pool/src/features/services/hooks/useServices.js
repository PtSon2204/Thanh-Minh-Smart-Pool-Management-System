import { useQuery } from '@tanstack/react-query'
import serviceService from '../services/serviceService'

export const SERVICES_QUERY_KEY = ['services']
export const INVENTORY_HISTORY_QUERY_KEY = ['inventory-history']

export function useServices(params) {
  return useQuery({
    queryKey: [...SERVICES_QUERY_KEY, params],
    queryFn: () => serviceService.getAll(params),
  })
}

export function useInventoryHistory(serviceId, params, enabled) {
  return useQuery({
    queryKey: [...INVENTORY_HISTORY_QUERY_KEY, serviceId, params],
    queryFn: () => serviceService.getInventoryHistory(serviceId, params),
    enabled,
  })
}
