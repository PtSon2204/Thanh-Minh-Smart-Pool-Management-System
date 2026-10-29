import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import inspectionService from '../services/inspectionService'

export function useInspectionForm() {
  return useQuery({
    queryKey: ['equipment-inspection-form'],
    queryFn: ({ signal }) => inspectionService.getForm(signal),
    staleTime: 60 * 1000,
  })
}

export function useSubmitInspection() {
  const queryClient = useQueryClient()
  return useMutation({
    mutationFn: (payload) => inspectionService.submit(payload),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['equipment-inspection-form'] })
      queryClient.invalidateQueries({ queryKey: ['equipment-inspections'] })
      queryClient.invalidateQueries({ queryKey: ['equipments'] })
      queryClient.invalidateQueries({ queryKey: ['maintenance-list'] })
    },
  })
}

export function useInspections(params) {
  return useQuery({
    queryKey: ['equipment-inspections', params],
    queryFn: ({ signal }) => inspectionService.getInspections(params, signal),
  })
}
