import { useQuery } from '@tanstack/react-query'
import inspectionService from '../services/inspectionService'
import { useAuthStore } from '../../auth/store/authStore'

export function useMandatoryChecklistStatus() {
  const session = useAuthStore((state) => state.session)
  const isLifeguard = session?.role?.toUpperCase() === 'LIFEGUARD'

  return useQuery({
    queryKey: ['mandatory-checklist-status', session?.id],
    queryFn: ({ signal }) => inspectionService.getPendingMandatoryStatus(signal),
    enabled: Boolean(isLifeguard && session?.id),
    staleTime: 15 * 1000,
    refetchInterval: 60 * 1000,
  })
}

export default useMandatoryChecklistStatus
