import { useQuery } from '@tanstack/react-query'
import equipmentService from '../services/equipmentService'

export function useEquipments(params) {
  return useQuery({
    queryKey: ['equipments', params],
    queryFn: ({ signal }) => equipmentService.getAll(params, signal),
    refetchInterval: query => query.state.data?.items?.some(item => item.cameraConnection) ? 10000 : false,
  })
}

export function useEquipmentFilters() {
  return useQuery({
    queryKey: ['equipment-filters'],
    queryFn: ({ signal }) => equipmentService.getFilters(signal),
  })
}
