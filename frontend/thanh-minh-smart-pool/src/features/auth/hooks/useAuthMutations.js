import { useMutation } from '@tanstack/react-query'
import authService from '../services/authService'
import { useAuthStore } from '../store/authStore'

export function useLogin() {
  return useMutation({
    mutationFn: authService.login,
    retry: false,
    onSuccess: (session) => useAuthStore.getState().setSession(session),
  })
}

export function useRegister() {
  return useMutation({ mutationFn: authService.register, retry: false })
}
