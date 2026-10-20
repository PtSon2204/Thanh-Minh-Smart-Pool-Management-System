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

export function useVerifyRegistration() {
  return useMutation({ mutationFn: authService.verifyRegistration, retry: false })
}

export function useResendRegistration() {
  return useMutation({ mutationFn: authService.resendRegistration, retry: false })
}

export function useRequestPasswordReset() {
  return useMutation({ mutationFn: authService.requestPasswordReset, retry: false })
}

export function useResendPasswordReset() {
  return useMutation({ mutationFn: authService.resendPasswordReset, retry: false })
}

export function useVerifyPasswordReset() {
  return useMutation({ mutationFn: authService.verifyPasswordReset, retry: false })
}

export function useCompletePasswordReset() {
  return useMutation({ mutationFn: authService.completePasswordReset, retry: false })
}
