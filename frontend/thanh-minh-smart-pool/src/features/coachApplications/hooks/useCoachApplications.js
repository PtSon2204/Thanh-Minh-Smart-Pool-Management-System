import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query'
import { coachApplicationService } from '../services/coachApplicationService'

export function useMyCoachApplication() {
  return useQuery({
    queryKey: ['coach-applications', 'me'],
    queryFn: ({ signal }) => coachApplicationService.getMine(signal),
  })
}

export function useMyCoachApplicationRequests(params) {
  return useQuery({
    queryKey: ['coach-applications', 'my-requests', params],
    queryFn: ({ signal }) => coachApplicationService.getMyRequests(params, signal),
  })
}

export function useCoachApplicationList(params) {
  return useQuery({
    queryKey: ['coach-applications', 'list', params],
    queryFn: ({ signal }) => coachApplicationService.getList(params, signal),
  })
}

export function useCoachApplicationDetail(id) {
  return useQuery({
    queryKey: ['coach-applications', 'detail', id],
    queryFn: ({ signal }) => coachApplicationService.getById(id, signal),
    enabled: Boolean(id),
  })
}

export function useCoachCertificate(id) {
  return useQuery({
    queryKey: ['coach-applications', 'certificate', id],
    queryFn: ({ signal }) => coachApplicationService.getCertificate(id, signal),
    enabled: Boolean(id),
    staleTime: 0,
    gcTime: 0,
    refetchOnWindowFocus: true,
  })
}

export function useSubmitCoachApplication() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: coachApplicationService.submit,
    onSuccess: () => {
      client.invalidateQueries({ queryKey: ['coach-applications', 'me'] })
      client.invalidateQueries({ queryKey: ['coach-applications', 'my-requests'] })
    },
  })
}

export function useReviewCoachApplication() {
  const client = useQueryClient()
  return useMutation({
    mutationFn: ({ id, decision, note }) => coachApplicationService.review(id, decision, note),
    onSuccess: (_, variables) => {
      client.invalidateQueries({ queryKey: ['coach-applications', 'list'] })
      client.invalidateQueries({ queryKey: ['coach-applications', 'detail', variables.id] })
    },
  })
}

